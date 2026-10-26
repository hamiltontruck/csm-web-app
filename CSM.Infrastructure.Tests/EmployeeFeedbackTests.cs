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

public sealed class EmployeeFeedbackTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");

    [Theory]
    [InlineData("company", 401)]
    [InlineData("company", 403)]
    [InlineData("company", 500)]
    [InlineData("site", 401)]
    [InlineData("site", 403)]
    [InlineData("site", 500)]
    [InlineData("assigned", 401)]
    [InlineData("assigned", 403)]
    [InlineData("assigned", 500)]
    public void Employee_scopes_show_safe_errors_and_withhold_actions(string scope, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, scope);
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? "You do not have permission to view employees."
            : "Unable to load employees. Please try again.";
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("Add Employee", page.Markup);
        Assert.Empty(page.FindAll("table, select, .spinner-border"));
        Assert.Equal(status == 401, page.FindAll("#employees-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-employees").Count == 1);
        Assert.Contains("Back", page.Markup);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public void Direct_create_checks_protected_access_before_rendering_form(int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EmployeeCreate>();
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? "You do not have permission to create employees."
            : "Unable to load employee form. Please try again.";
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea, .spinner-border"));
        Assert.Equal(status == 401, page.FindAll("#employee-create-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-employee-create").Count == 1);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Contains("Back", page.Markup);
    }

    [Theory]
    [InlineData("company", "Test Employee")]
    [InlineData("site", "Hamilton Test Site")]
    [InlineData("assigned", "Active employee")]
    public void Retry_recovers_the_current_scope_and_preserves_filters(string scope, string expected)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, scope);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-employees")));
        state.Status = null;
        page.Find("#retry-employees").Click();
        page.WaitForAssertion(() => Assert.Contains(expected, page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.DoesNotContain("Inactive employee", page.Markup);
        Assert.Contains("Add Employee", page.Markup);
    }

    [Fact]
    public void Successful_empty_site_list_is_an_empty_state_not_a_technical_error()
    {
        using var context = Context(out var state);
        state.EmptySites = true;
        var page = Render(context, "site");
        page.WaitForAssertion(() => Assert.Contains("No active construction sites found.", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.DoesNotContain("GetSitesAsync", page.Markup);
    }

    [Fact]
    public void Successful_create_access_renders_the_form()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<EmployeeCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("Employee Number", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    private static IRenderedComponent<Employees> Render(TestContext context, string scope) =>
        context.RenderComponent<Employees>(p => {
            if (scope == "assigned") p.Add(x => x.SiteId, SiteId);
            else p.Add(x => x.Scope, scope);
        });

    private static TestContext Context(out State state)
    {
        var context = new TestContext();
        state = new State();
        var employee = DispatchProxy.Create<IEmployeeService, EmployeeApi>();
        ((EmployeeApi)(object)employee).State = state;
        var sites = DispatchProxy.Create<IProjectService, SitesApi>();
        ((SitesApi)(object)sites).State = state;
        var assignments = DispatchProxy.Create<ISiteAssignmentService, AssignmentApi>();
        ((AssignmentApi)(object)assignments).State = state;
        context.Services.AddSingleton(employee);
        context.Services.AddSingleton(sites);
        context.Services.AddSingleton(assignments);
        return context;
    }
    public sealed class State { public HttpStatusCode? Status; public bool EmptySites; }
    public class EmployeeApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IEmployeeService.GetEmployeesAsync)) throw new InvalidOperationException("Unexpected employee mutation.");
            return State.Status.HasValue
                ? Task.FromException<IReadOnlyList<EmployeeResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyList<EmployeeResponse>>(new List<EmployeeResponse> { new() { FirstName = "Test", LastName = "Employee" } });
        }
    }
    public class SitesApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => State.Status.HasValue
            ? Task.FromException<List<SiteResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
            : Task.FromResult(State.EmptySites ? new List<SiteResponse>() : new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }
    public class AssignmentApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if ((Guid?)args![1] != SiteId) throw new InvalidOperationException("Wrong site filter.");
            return State.Status.HasValue
                ? Task.FromException<IReadOnlyCollection<SiteAssignmentResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyCollection<SiteAssignmentResponse>>(new List<SiteAssignmentResponse> {
                    new() { IsActive = true, EmployeeName = "Active employee" },
                    new() { IsActive = false, EmployeeName = "Inactive employee" }
                });
        }
    }
}
