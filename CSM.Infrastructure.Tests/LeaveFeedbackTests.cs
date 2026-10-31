using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class LeaveFeedbackTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true, true)]
    [InlineData(AppRoles.CompanyAdmin, true, true)]
    [InlineData(AppRoles.SiteManager, true, true)]
    [InlineData(AppRoles.Supervisor, true, false)]
    [InlineData(AppRoles.HrOfficer, true, true)]
    [InlineData(AppRoles.HROfficer, true, true)]
    [InlineData(AppRoles.Administrator, false, false)]
    [InlineData(AppRoles.Storekeeper, false, false)]
    [InlineData(AppRoles.Employee, false, false)]
    public async Task Controller_matches_existing_read_create_and_review_cancel_roles(string role, bool submit, bool review)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Review", "Cancel" })
        {
            var metadata = typeof(LeavesController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(LeavesController).GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(method is "Review" or "Cancel" ? review : submit,
                (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!)).Succeeded);
        }
    }
    [Fact]
    public async Task Supervisor_reads_only_company_leave_requests()
    {
        using var db = Database(out var userId, out _);
        var result = await Controller(db, userId).GetAll(null, null, null, default);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<CSM.Application.HRM.Leaves.Dtos.LeaveResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Id, Assert.Single(rows).Id);
    }
    [Fact]
    public async Task Supervisor_review_and_cancel_are_403_and_leave_status_is_unchanged()
    {
        using var db = Database(out var userId, out _);
        var controller = Controller(db, userId);
        var review = await controller.Review(Id, new(LeaveStatus.Approved, null), default);
        var cancel = await controller.Cancel(Id, new(null), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(review.Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(cancel.Result).StatusCode);
        Assert.All(db.LeaveRequests, x => Assert.Equal(LeaveStatus.Pending, x.Status));
    }
    [Fact]
    public async Task Foreign_company_leave_read_is_403()
    {
        using var db = Database(out var userId, out var foreignId);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await Controller(db, userId).GetById(foreignId, default)).Result).StatusCode);
    }
    [Fact]
    public async Task Invalid_date_filter_keeps_HTTP_400()
    {
        using var db = Database(out var userId, out _);
        var result = await Controller(db, userId).GetAll(null, new(2026, 10, 27), new(2026, 10, 26), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 403)]
    [InlineData(false, 500)]
    [InlineData(true, 401)]
    [InlineData(true, 403)]
    [InlineData(true, 500)]
    public void Load_failures_show_safe_feedback_and_withhold_actions_and_forms(bool create, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, create);
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? (create ? "You do not have permission to create leave requests." : "You do not have permission to view leave requests.")
            : create ? "Unable to load leave request form. Please try again." : "Unable to load leave requests. Please try again.";
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("table, form, input, select, textarea"));
        Assert.DoesNotContain("Add Leave", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("No leave requests found", page.Markup);
        var suffix = create ? "create" : "list";
        Assert.Equal(status == 401, page.FindAll("#leave-" + suffix + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-leave-" + suffix).Count == 1);
        Assert.Contains("Back", page.Markup);
    }
    [Theory]
    [InlineData(false, "Add Leave")]
    [InlineData(true, "Create Leave")]
    public void Retry_recovers_allowed_data_without_submitting_a_request(bool create, string expected)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, create);
        var selector = create ? "#retry-leave-create" : "#retry-leave-list";
        page.WaitForAssertion(() => Assert.Single(page.FindAll(selector)));
        state.Status = null;
        page.Find(selector).Click();
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Contains(expected, page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }
    [Fact]
    public void Submit_only_user_sees_requests_and_add_but_no_review_or_cancel_buttons()
    {
        using var context = Context(out var state);
        state.CanReview = false;
        var page = Render(context, false);
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Contains("Add Leave", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Approve" or "Reject" or "Cancel");
    }
    [Fact]
    public void Direct_create_requires_submit_permission()
    {
        using var context = Context(out var state);
        state.CanSubmit = false;
        var page = Render(context, true);
        page.WaitForAssertion(() => Assert.Equal("You do not have permission to create leave requests.", page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-leave-create"));
    }
    [Fact]
    public void Employee_options_denial_has_no_partial_form()
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = Render(context, true);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-leave-create"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }
    [Theory]
    [InlineData("Approve")]
    [InlineData("Cancel")]
    public void Action_denials_are_caught_without_crashing_the_page(string action)
    {
        using var context = Context(out var state);
        state.ActionDenied = true;
        var page = Render(context, false);
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        page.FindAll("button").Single(b => b.TextContent.Trim() == action).Click();
        page.WaitForAssertion(() => Assert.Equal("You do not have permission to review or cancel leave requests.", page.Find("[role=alert]").TextContent.Trim()));
        Assert.DoesNotContain("Add Leave", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }
    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 403)]
    [InlineData(false, 500)]
    [InlineData(true, 401)]
    [InlineData(true, 403)]
    [InlineData(true, 500)]
    public async Task Action_clients_preserve_status_without_exposing_response_body(bool cancel, int status)
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new CSM.Web.Services.LeaveService(new Factory(http), context.Services.GetRequiredService<ProtectedSessionStorage>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () => {
            if (cancel) await service.CancelAsync(Id, new());
            else await service.ReviewAsync(Id, new() { Status = "Approved" });
        });
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    private static ApplicationDbContext Database(out Guid userId, out Guid foreignId)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        userId = Guid.NewGuid(); var companyId = Guid.NewGuid(); foreignId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, IsActive = true, CompanyId = companyId, UserRoles = [
            new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = AppRoles.Supervisor } } ] });
        foreach (var pair in new[] { (Id, companyId), (foreignId, Guid.NewGuid()) })
            db.LeaveRequests.Add(new LeaveRequest { Id = pair.Item1, CompanyId = pair.Item2, LeaveType = "Annual Leave",
                StartDate = new(2026, 10, 26), EndDate = new(2026, 10, 26), NumberOfDays = 1,
                Employee = new Employee { Id = Guid.NewGuid(), CompanyId = pair.Item2, EmployeeNumber = pair.Item1.ToString(), FirstName = "Test", LastName = "Employee" } });
        db.SaveChanges(true); return db;
    }
    private static LeavesController Controller(ApplicationDbContext db, Guid userId) =>
        new(new CSM.Infrastructure.Services.LeaveService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) } } };
    [Fact]
    public void Supervisor_with_review_permission_can_read_but_cannot_review_or_cancel()
    {
        using var context = Context(out var state);
        state.Role = "Supervisor";
        var page = Render(context, false);
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Approve" or "Reject" or "Cancel");
    }

    private static IRenderedFragment Render(TestContext context, bool create) =>
        create ? context.RenderComponent<LeaveCreate>() : context.RenderComponent<Leave>();
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new State();
        var api = DispatchProxy.Create<ILeaveService, Api>(); ((Api)(object)api).State = state;
        var employee = DispatchProxy.Create<IEmployeeService, EmployeeApi>(); ((EmployeeApi)(object)employee).State = state;
        var auth = DispatchProxy.Create<IAuthService, Auth>(); ((Auth)(object)auth).State = state;
        context.Services.AddSingleton(api); context.Services.AddSingleton(employee); context.Services.AddSingleton(auth); return context;
    }
    public sealed class State { public string Role = "CompanyAdmin"; public HttpStatusCode? Status; public bool CanSubmit = true; public bool CanReview = true; public bool OptionsDenied; public bool ActionDenied; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(ILeaveService.GetEmployeeOptionsAsync) => State.OptionsDenied
                ? Task.FromException<IReadOnlyCollection<LeaveEmployeeOptionResponse>>(new HttpRequestException("private diagnostic", null, HttpStatusCode.Forbidden))
                : Task.FromResult<IReadOnlyCollection<LeaveEmployeeOptionResponse>>(new List<LeaveEmployeeOptionResponse> { new() { Id = Id, EmployeeNumber = "EMP-001", DisplayName = "Test Employee" } }),
            nameof(ILeaveService.GetAllAsync) => State.Status.HasValue
                ? Task.FromException<IReadOnlyCollection<LeaveResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyCollection<LeaveResponse>>(new List<LeaveResponse> { new() { Id = Id, EmployeeName = "Test Employee", Status = "Pending" } }),
            nameof(ILeaveService.ReviewAsync) or nameof(ILeaveService.CancelAsync) when State.ActionDenied =>
                Task.FromException<LeaveResponse>(new HttpRequestException("private diagnostic", null, HttpStatusCode.Forbidden)),
            _ => throw new InvalidOperationException("Unexpected leave mutation.")
        };
    }
    public class EmployeeApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => State.OptionsDenied
            ? Task.FromException<IReadOnlyList<EmployeeResponse>>(new HttpRequestException("private diagnostic", null, HttpStatusCode.Forbidden))
            : Task.FromResult<IReadOnlyList<EmployeeResponse>>(new List<EmployeeResponse> { new() { FirstName = "Test Employee" } });
    }
    public class Auth : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(method!.Name == nameof(IAuthService.GetRolesAsync) ? new[] { State.Role } : new[] { "HRM_Leave_View",
                State.CanSubmit ? "HRM_Leave_Submit" : "",
                State.CanReview ? "HRM_Leave_Approve" : "" });
    }
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Response(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private diagnostic") });
    }
}
