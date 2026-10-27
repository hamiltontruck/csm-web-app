using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CSM.Infrastructure.Persistence;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class HrTimesheetFeedbackTests
{
    [Theory]
    [InlineData("SuperAdmin", true, true, true)]
    [InlineData("CompanyAdmin", true, true, true)]
    [InlineData("SiteManager", true, true, true)]
    [InlineData("Supervisor", true, false, false)]
    [InlineData("HrOfficer", true, true, false)]
    [InlineData("HROfficer", true, true, false)]
    [InlineData("Administrator", false, false, false)]
    [InlineData("Storekeeper", false, false, false)]
    public async Task Endpoint_roles_match_existing_service_contract(string role, bool read, bool manage, bool approve)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Generate", "Submit", "Cancel", "Review" })
        {
            var metadata = typeof(TimesheetsController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(TimesheetsController).GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(method is "GetAll" or "GetById" ? read : method == "Review" ? approve : manage,
                (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
        }
    }

    [Theory]
    [InlineData("list", 401)] [InlineData("list", 403)] [InlineData("list", 500)]
    [InlineData("create", 401)] [InlineData("create", 403)] [InlineData("create", 500)]
    [InlineData("edit", 401)] [InlineData("edit", 403)] [InlineData("edit", 500)]
    public void Failed_load_withholds_empty_state_and_fields(string route, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("table, form, input, select, textarea"));
        Assert.DoesNotContain("No timesheets found", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Create Timesheet");
        Assert.Equal(status == 401, page.FindAll("#timesheet-" + route + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-timesheet-" + route).Count == 1);
    }

    [Theory]
    [InlineData("list")] [InlineData("create")] [InlineData("edit")]
    public void Retry_recovers_protected_data_without_mutating(string route)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-timesheet-" + route)));
        state.Status = null;
        page.Find("#retry-timesheet-" + route).Click();
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public void Supervisor_can_read_but_has_no_create_or_workflow_controls()
    {
        using var context = Context(out var state);
        state.Role = "Supervisor";
        var list = Render(context, "list");
        list.WaitForAssertion(() => Assert.Contains("Test Employee", list.Markup));
        Assert.DoesNotContain(list.FindAll("button"), b => b.TextContent.Trim() == "Create Timesheet");
        var detail = Render(context, "edit");
        detail.WaitForAssertion(() => Assert.Contains("Test Employee", detail.Markup));
        Assert.Empty(detail.FindAll("form"));
        Assert.DoesNotContain(detail.FindAll("button"), b => b.TextContent.Trim() is "Approve" or "Reject" or "Cancel Timesheet" or "Submit Timesheet");
        var create = Render(context, "create");
        create.WaitForAssertion(() => Assert.Single(create.FindAll("[role=alert]")));
        Assert.Empty(create.FindAll("form, input, select, textarea, #retry-timesheet-create"));
    }

    [Fact]
    public void Denied_employee_options_never_expose_partial_form()
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = Render(context, "create");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-timesheet-create"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Fact]
    public void Missing_detail_is_not_an_empty_form_or_retry()
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Contains("Timesheet was not found.", page.Markup));
        Assert.Empty(page.FindAll("form, input, textarea, #retry-timesheet-edit"));
    }

    [Fact]
    public void Failed_workflow_action_hides_stale_controls_and_raw_error()
    {
        using var context = Context(out var state);
        state.ActionDenied = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == "Submit Timesheet"));
        page.FindAll("form").First().Submit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, textarea"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Fact]
    public async Task Supervisor_reads_only_company_rows_and_foreign_company_detail_is_denied()
    {
        using var db = Database(out var actorId, out var ownId, out var foreignId);
        var controller = Controller(db, actorId);
        var result = await controller.GetAll();
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<CSM.Application.HRM.Timesheets.Dtos.TimesheetResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(ownId, Assert.Single(rows).Id);
        var foreign = await controller.GetById(foreignId, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(foreign.Result).StatusCode);
    }

    [Fact]
    public async Task Supervisor_cannot_cancel_and_existing_timesheets_are_unchanged()
    {
        using var db = Database(out var actorId, out var ownId, out _);
        var result = await Controller(db, actorId).Cancel(ownId, new(null), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.All(db.Timesheets, row => Assert.Equal(TimesheetStatus.Draft, row.Status));
    }

    private static ApplicationDbContext Database(out Guid actorId, out Guid ownId, out Guid foreignId)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        actorId = Guid.NewGuid(); ownId = Guid.NewGuid(); foreignId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        db.Users.Add(new User { Id = actorId, IsActive = true, CompanyId = companyId, UserRoles = [
            new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = "Supervisor" } } ] });
        foreach (var pair in new[] { (ownId, companyId), (foreignId, Guid.NewGuid()) })
            db.Timesheets.Add(new CSM.Domain.Entities.HRM.Timesheet { Id = pair.Item1, CompanyId = pair.Item2,
                PeriodStartDate = new(2026, 10, 1), PeriodEndDate = new(2026, 10, 31), Status = TimesheetStatus.Draft,
                Employee = new Employee { Id = Guid.NewGuid(), CompanyId = pair.Item2, EmployeeNumber = pair.Item1.ToString(), FirstName = "Test", LastName = "Employee" } });
        db.SaveChanges(true); return db;
    }

    private static TimesheetsController Controller(ApplicationDbContext db, Guid actorId) =>
        new(new CSM.Infrastructure.Services.TimesheetService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "test")) } } };

    private static IRenderedFragment Render(TestContext context, string route) => route switch {
        "list" => context.RenderComponent<Timesheets>(),
        "create" => context.RenderComponent<TimesheetCreate>(),
        _ => context.RenderComponent<TimesheetEdit>(p => p.Add(x => x.TimesheetId, Guid.Parse("11111111-1111-1111-1111-111111111111")))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new();
        var api = DispatchProxy.Create<ITimesheetService, Api>(); ((Api)(object)api).State = state;
        var employee = DispatchProxy.Create<IEmployeeService, EmployeeApi>(); ((EmployeeApi)(object)employee).State = state;
        var auth = DispatchProxy.Create<IAuthService, Auth>(); ((Auth)(object)auth).State = state;
        context.Services.AddSingleton(api); context.Services.AddSingleton(employee); context.Services.AddSingleton(auth);
        return context;
    }
    public sealed class State { public HttpStatusCode? Status; public string Role = "SiteManager"; public bool OptionsDenied; public bool Missing; public bool ActionDenied; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var row = new TimesheetResponse { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), EmployeeName = "Test Employee", Status = "Draft" };
            if (method!.Name == nameof(ITimesheetService.GetAllAsync))
                return State.Status.HasValue ? Task.FromException<IReadOnlyCollection<TimesheetResponse>>(Error(State.Status.Value))
                    : Task.FromResult<IReadOnlyCollection<TimesheetResponse>>(new[] { row });
            if (method.Name == nameof(ITimesheetService.GetByIdAsync))
                return State.Status.HasValue ? Task.FromException<TimesheetResponse?>(Error(State.Status.Value))
                    : Task.FromResult<TimesheetResponse?>(State.Missing ? null : row);
            if (State.ActionDenied) return Task.FromException<TimesheetResponse>(Error(HttpStatusCode.Forbidden));
            throw new InvalidOperationException("Unexpected timesheet mutation.");
        }
    }
    public class EmployeeApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => State.OptionsDenied
            ? Task.FromException<IReadOnlyList<EmployeeResponse>>(Error(HttpStatusCode.Forbidden))
            : Task.FromResult<IReadOnlyList<EmployeeResponse>>(new[] { new EmployeeResponse { FirstName = "Test Employee" } });
    }
    public class Auth : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult<IReadOnlyCollection<string>>(new[] { State.Role });
    }
    private static HttpRequestException Error(HttpStatusCode status) => new("private diagnostic", null, status);
}
