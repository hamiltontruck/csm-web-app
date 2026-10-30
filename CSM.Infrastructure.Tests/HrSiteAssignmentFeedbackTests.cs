using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using CSM.Web.Services.HRM;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class HrSiteAssignmentFeedbackTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    [Theory]
    [InlineData("list", 401)]
    [InlineData("list", 403)]
    [InlineData("list", 500)]
    [InlineData("create", 401)]
    [InlineData("create", 403)]
    [InlineData("create", 500)]
    [InlineData("edit", 401)]
    [InlineData("edit", 403)]
    [InlineData("edit", 500)]
    public void Failed_loads_show_safe_feedback_and_withhold_controls(string kind, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, kind);
        var denied = kind == "list" ? "You do not have permission to view site assignments."
            : kind == "create" ? "You do not have permission to create site assignments."
            : "You do not have permission to edit site assignments.";
        var fallback = kind == "list" ? "Unable to load site assignments. Please try again."
            : kind == "create" ? "Unable to load site assignment form. Please try again."
            : "Unable to load site assignment. Please try again.";
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? denied : fallback;
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("table, input, select, textarea"));
        Assert.DoesNotContain("Add Assignment", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("No site assignments found", page.Markup);
        Assert.Equal(status == 401, page.FindAll("#hr-assignment-" + kind + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-hr-assignment-" + kind).Count == 1);
        Assert.Contains("Back", page.Markup);
        if (status == 401)
        {
            var route = kind == "list" ? "/hr/site-assignments"
                : kind == "create" ? "/hr/site-assignments/create" : $"/hr/site-assignments/edit/{Id}";
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString(route),
                page.Find("#hr-assignment-" + kind + "-sign-in").GetAttribute("href"));
        }
    }

    [Theory]
    [InlineData("list", "Add Assignment")]
    [InlineData("create", "Save Assignment")]
    [InlineData("edit", "Save Changes")]
    public void Retry_recovers_allowed_data_without_creating_assignments(string kind, string expected)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, kind);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-hr-assignment-" + kind)));
        state.Status = null;
        page.Find("#retry-hr-assignment-" + kind).Click();
        page.WaitForAssertion(() => Assert.Contains(expected, page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Contains("Test Employee", page.Markup);
        Assert.Contains("Hamilton Test Site", page.Markup);
        if (kind == "create") Assert.DoesNotContain("Inactive site", page.Markup);
    }

    [Fact]
    public void View_only_access_preserves_the_list_but_hides_manage_actions()
    {
        using var context = Context(out var state);
        state.CanManage = false;
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.DoesNotContain("Add Assignment", page.Markup);
        Assert.DoesNotContain(">Edit<", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Edit");
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData("create", "You do not have permission to create site assignments.")]
    [InlineData("edit", "You do not have permission to edit site assignments.")]
    public void Direct_forms_require_manage_permission_after_successful_protected_reads(string kind, string expected)
    {
        using var context = Context(out var state);
        state.CanManage = false;
        var page = Render(context, kind);
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("input, select, textarea"));
        Assert.Empty(page.FindAll("#retry-hr-assignment-" + kind));
    }

    [Fact]
    public void Missing_edit_record_has_no_form_or_retry()
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Equal("Site assignment was not found.", page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("input, select, textarea, #retry-hr-assignment-edit"));
    }

    [Fact]
    public void Supervisor_with_manage_permission_still_has_read_only_site_assignments()
    {
        using var context = Context(out var state);
        state.Role = "Supervisor";
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Add Assignment") || b.TextContent.Trim() == "Edit");
    }

    private static IRenderedFragment Render(TestContext context, string kind) => kind switch {
        "list" => context.RenderComponent<SiteAssignments>(),
        "create" => context.RenderComponent<SiteAssignmentCreate>(),
        _ => context.RenderComponent<SiteAssignmentEdit>(p => p.Add(x => x.AssignmentId, Id))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext();
        state = new State();
        var api = DispatchProxy.Create<ISiteAssignmentService, Assignments>();
        ((Assignments)(object)api).State = state;
        var employee = DispatchProxy.Create<IEmployeeService, EmployeesApi>();
        ((EmployeesApi)(object)employee).State = state;
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        ((Auth)(object)auth).State = state;
        context.Services.AddSingleton(api);
        context.Services.AddSingleton(employee);
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        return context;
    }
    public sealed class State { public string Role = "CompanyAdmin"; public HttpStatusCode? Status; public bool CanManage = true; public bool Missing; }
    private static SiteAssignmentResponse Record() => new() { Id = Id, EmployeeName = "Test Employee", SiteName = "Hamilton Test Site", IsActive = true };
    public class Assignments : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(ISiteAssignmentService.GetAllAsync) => State.Status.HasValue
                ? Task.FromException<IReadOnlyCollection<SiteAssignmentResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyCollection<SiteAssignmentResponse>>(new List<SiteAssignmentResponse> { Record() }),
            nameof(ISiteAssignmentService.GetByIdAsync) => State.Status.HasValue
                ? Task.FromException<SiteAssignmentResponse?>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<SiteAssignmentResponse?>(State.Missing ? null : Record()),
            _ => throw new InvalidOperationException("Unexpected assignment mutation.")
        };
    }
    public class EmployeesApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IEmployeeService.GetEmployeesAsync) || args![0]?.ToString() != "Active")
                throw new InvalidOperationException("Expected active employee options.");
            return State.Status.HasValue
                ? Task.FromException<IReadOnlyList<EmployeeResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyList<EmployeeResponse>>(new List<EmployeeResponse> { new() { Id = Id, FirstName = "Test Employee" } });
        }
    }
    public class Sites : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> {
                new() { Name = "Hamilton Test Site", IsActive = true },
                new() { Name = "Inactive site", IsActive = false }
            });
    }
    public class Auth : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(method!.Name == nameof(IAuthService.GetRolesAsync) ? new[] { State.Role } : State.CanManage
                ? new[] { "HRM_SiteAssignment_View", "HRM_SiteAssignment_Manage" }
                : new[] { "HRM_SiteAssignment_View" });
    }
}
