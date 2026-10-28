using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AttendanceFeedbackTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");
    [Theory]
    [InlineData("list", 401)]
    [InlineData("list", 403)]
    [InlineData("list", 500)]
    [InlineData("check-in", 401)]
    [InlineData("check-in", 403)]
    [InlineData("check-in", 500)]
    [InlineData("check-out", 401)]
    [InlineData("check-out", 403)]
    [InlineData("check-out", 500)]
    [InlineData("override", 401)]
    [InlineData("override", 403)]
    [InlineData("override", 500)]
    public void Attendance_load_errors_are_safe_and_do_not_expose_actions_or_fields(string kind, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, kind);
        var denied = kind switch {
            "list" => "You do not have permission to view attendance records.",
            "check-in" => "You do not have permission to record attendance check-ins.",
            "check-out" => "You do not have permission to record attendance check-outs.",
            _ => "You do not have permission to override attendance records."
        };
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? denied
            : kind == "list" ? "Unable to load attendance records. Please try again."
            : "Unable to load attendance form. Please try again.";
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("No attendance records found", page.Markup);
        Assert.Empty(page.FindAll("table, input, select, textarea, .spinner-border"));
        Assert.Equal(status == 401, page.FindAll("#attendance-" + kind + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-attendance-" + kind).Count == 1);
        Assert.Contains("Back", page.Markup);
        if (kind == "list") Assert.DoesNotContain("Check In", page.Markup);
        if (status == 401)
        {
            var route = kind == "list" ? "/hr/attendance"
                : kind == "check-in" ? "/hr/attendance/check-in" : $"/hr/attendance/{kind}/{Id}";
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString(route),
                page.Find("#attendance-" + kind + "-sign-in").GetAttribute("href"));
        }
    }

    [Theory]
    [InlineData("list")]
    [InlineData("check-in")]
    [InlineData("check-out")]
    [InlineData("override")]
    public void Retry_restores_records_or_form_options_without_writing_attendance(string kind)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, kind);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-attendance-" + kind)));
        state.Status = null;
        page.Find("#retry-attendance-" + kind).Click();
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        if (kind == "list") Assert.Contains("Check In", page.Markup);
        else Assert.NotEmpty(page.FindAll("input, select, textarea"));
        if (kind == "check-in") Assert.Contains("Hamilton Test Site", page.Markup);
    }

    [Fact]
    public void Check_in_dependency_denial_withholds_partially_loaded_fields()
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = Render(context, "check-in");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("input, select, textarea"));
        Assert.Empty(page.FindAll("#retry-attendance-check-in"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Fact]
    public void Approval_denial_has_safe_feedback_and_hides_mutation_actions()
    {
        using var context = Context(out var state);
        state.ApprovalCase = true;
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Approve").Click();
        page.WaitForAssertion(() => Assert.Equal("You do not have permission to approve attendance.",
            page.Find("[role=alert]").TextContent.Trim()));
        Assert.DoesNotContain("Check In", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Empty(page.FindAll("#retry-attendance-list"));
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("CompanyAdmin", true)]
    [InlineData("SiteManager", true)]
    [InlineData("Supervisor", false)]
    [InlineData("HrOfficer", false)]
    [InlineData("HROfficer", false)]
    public void Read_record_access_does_not_grant_review_buttons(string role, bool review)
    {
        using var context = Context(out var state);
        state.Role = role;
        state.ApprovalCase = true;
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Contains("Check In", page.Markup);
        Assert.Equal(review, page.FindAll("button").Any(b => b.TextContent.Trim() == "Approve"));
        Assert.Equal(review, page.FindAll("button").Any(b => b.TextContent.Trim() == "Override"));
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("CompanyAdmin", true)]
    [InlineData("SiteManager", true)]
    [InlineData("Supervisor", false)]
    [InlineData("HrOfficer", false)]
    [InlineData("HROfficer", false)]
    public void Direct_override_requires_existing_review_role(string role, bool review)
    {
        using var context = Context(out var state);
        state.Role = role;
        var page = Render(context, "override");
        if (review)
            page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("input, select, textarea")));
        else
        {
            page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
            Assert.Equal("You do not have permission to override attendance records.", page.Find("[role=alert]").TextContent.Trim());
            Assert.Empty(page.FindAll("input, select, textarea, #retry-attendance-override"));
        }
    }

    private static IRenderedFragment Render(TestContext context, string kind) => kind switch {
        "list" => context.RenderComponent<Attendance>(),
        "check-in" => context.RenderComponent<AttendanceCheckIn>(),
        "check-out" => context.RenderComponent<AttendanceCheckOut>(p => p.Add(x => x.AttendanceId, Id)),
        _ => context.RenderComponent<AttendanceOverride>(p => p.Add(x => x.AttendanceId, Id))
    };

    private static TestContext Context(out State state)
    {
        var context = new TestContext();
        state = new State();
        var attendance = DispatchProxy.Create<IAttendanceService, AttendanceApi>();
        ((AttendanceApi)(object)attendance).State = state;
        var employee = DispatchProxy.Create<IEmployeeService, EmployeeApi>();
        ((EmployeeApi)(object)employee).State = state;
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        ((Auth)(object)auth).State = state;
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(attendance);
        context.Services.AddSingleton(employee);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        return context;
    }
    public sealed class State { public string Role = "CompanyAdmin"; public HttpStatusCode? Status; public bool OptionsDenied; public bool ApprovalCase; }
    private static AttendanceResponse Record(bool approvedCase) => new() {
        Id = Id, EmployeeId = Id, ConstructionSiteId = SiteId, EmployeeName = "Test Employee",
        SiteName = "Hamilton Test Site", Status = "Present", Source = "Manual",
        CheckInAtUtc = DateTime.UtcNow.AddHours(-2), CheckOutAtUtc = approvedCase ? DateTime.UtcNow : null,
        RequiresApproval = approvedCase
    };
    public class Auth : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(new[] { State.Role });
    }
    public class AttendanceApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IAttendanceService.GetAttendancesAsync) => State.Status.HasValue
                ? Task.FromException<IReadOnlyList<AttendanceResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyList<AttendanceResponse>>(new List<AttendanceResponse> { Record(State.ApprovalCase) }),
            nameof(IAttendanceService.GetAttendanceAsync) => State.Status.HasValue
                ? Task.FromException<AttendanceResponse>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult(Record(false)),
            nameof(IAttendanceService.ApproveAsync) when State.ApprovalCase =>
                Task.FromException<AttendanceResponse>(new HttpRequestException("private diagnostic", null, HttpStatusCode.Forbidden)),
            _ => throw new InvalidOperationException("Unexpected attendance mutation.")
        };
    }
    public class EmployeeApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => State.OptionsDenied
            ? Task.FromException<IReadOnlyList<EmployeeResponse>>(new HttpRequestException("private diagnostic", null, HttpStatusCode.Forbidden))
            : Task.FromResult<IReadOnlyList<EmployeeResponse>>(new List<EmployeeResponse> { new() { Id = Id, FirstName = "Test", LastName = "Employee" } });
    }
    public class Sites : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }
}
