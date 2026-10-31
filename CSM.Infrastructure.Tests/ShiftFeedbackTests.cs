using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
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

public sealed class ShiftFeedbackTests
{
    private static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true, true)]
    [InlineData(AppRoles.CompanyAdmin, true, true)]
    [InlineData(AppRoles.SiteManager, true, false)]
    [InlineData(AppRoles.Supervisor, true, false)]
    [InlineData(AppRoles.HrOfficer, true, true)]
    [InlineData(AppRoles.HROfficer, true, true)]
    [InlineData(AppRoles.Administrator, false, false)]
    [InlineData(AppRoles.Storekeeper, false, false)]
    [InlineData(AppRoles.Employee, false, false)]
    public async Task Controller_read_and_write_roles_match_existing_service_contract(string role, bool read, bool write)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Update", "ChangeActiveStatus" })
        {
            var metadata = typeof(ShiftsController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(ShiftsController).GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(method.StartsWith("Get") ? read : write,
                (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!)).Succeeded);
        }
    }

    [Fact]
    public async Task Supervisor_reads_only_own_company_shifts()
    {
        using var db = Database(AppRoles.Supervisor, out var userId, out _);
        var result = await Controller(db, userId).GetAll(default);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<CSM.Application.HRM.Shifts.Dtos.ShiftResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("DAY", Assert.Single(rows).Code);
    }
    [Fact]
    public async Task Supervisor_write_denial_is_403_without_creating_a_shift()
    {
        using var db = Database(AppRoles.Supervisor, out var userId, out _);
        var result = await Controller(db, userId).Create(new("NEW", "New", new(8, 0), new(17, 0), 0, 8m, false), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(2, db.Shifts.Count());
    }
    [Fact]
    public async Task Cross_company_read_denial_is_403()
    {
        using var db = Database(AppRoles.Supervisor, out var userId, out var foreignId);
        var result = await Controller(db, userId).GetById(foreignId, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }
    [Fact]
    public async Task Shift_validation_remains_HTTP_400()
    {
        using var db = Database(AppRoles.CompanyAdmin, out var userId, out _);
        var result = await Controller(db, userId).Create(new("NEW", "New", default, default, 0, 8m, false), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(2, db.Shifts.Count());
    }

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
    public void Failed_loads_have_safe_feedback_and_no_mutation_controls(string kind, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, kind);
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? (kind == "list" ? "You do not have permission to view shifts." : $"You do not have permission to {kind} shifts.")
            : kind == "list" ? "Unable to load shifts. Please try again." : "Unable to load shift form. Please try again.";
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("table, form, input, select, textarea, .spinner-border"));
        Assert.DoesNotContain("Add Shift", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("No shifts have been configured", page.Markup);
        Assert.Equal(status == 401, page.FindAll("#shift-" + kind + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-shift-" + kind).Count == 1);
        Assert.Contains("Back", page.Markup);
    }
    [Theory]
    [InlineData("list", "Add Shift")]
    [InlineData("create", "Save Shift")]
    [InlineData("edit", "Save Changes")]
    public void Retry_recovers_the_allowed_shift_view_or_form(string kind, string expected)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, kind);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-shift-" + kind)));
        state.Status = null;
        page.Find("#retry-shift-" + kind).Click();
        page.WaitForAssertion(() => Assert.Contains(expected, page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        if (kind != "create") Assert.Contains("Day shift", page.Markup);
    }
    [Fact]
    public void Read_only_user_sees_shift_data_without_create_edit_or_status_buttons()
    {
        using var context = Context(out var state);
        state.CanSubmit = false;
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Day shift", page.Markup));
        Assert.DoesNotContain("Add Shift", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Edit" or "Activate" or "Deactivate");
        Assert.Empty(page.FindAll("[role=alert]"));
    }
    [Theory]
    [InlineData("create")]
    [InlineData("edit")]
    public void Direct_mutation_forms_require_submit_permission(string kind)
    {
        using var context = Context(out var state);
        state.CanSubmit = false;
        var page = Render(context, kind);
        page.WaitForAssertion(() => Assert.Equal($"You do not have permission to {kind} shifts.", page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-shift-" + kind));
    }
    [Fact]
    public void Missing_edit_shift_has_no_form_or_retry()
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Equal("Shift not found.", page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, #retry-shift-edit"));
    }
    [Fact]
    public void Status_denial_has_safe_feedback_and_hides_actions()
    {
        using var context = Context(out var state);
        state.ActivationDenied = true;
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Day shift", page.Markup));
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Deactivate").Click();
        page.WaitForAssertion(() => Assert.Equal("You do not have permission to change shift status.", page.Find("[role=alert]").TextContent.Trim()));
        Assert.DoesNotContain("Add Shift", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Status_client_preserves_HTTP_status_without_exposing_API_body(int status)
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new CSM.Web.Services.ShiftService(new Factory(http), context.Services.GetRequiredService<ProtectedSessionStorage>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.ChangeActiveStatusAsync(Id, new() { IsActive = false }));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    private static ApplicationDbContext Database(string role, out Guid userId, out Guid foreignId)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        userId = Guid.NewGuid(); var companyId = Guid.NewGuid(); foreignId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, IsActive = true, CompanyId = companyId, UserRoles = [
            new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } } ] });
        db.Shifts.AddRange(new Shift { Id = Id, CompanyId = companyId, Code = "DAY", Name = "Day shift" },
            new Shift { Id = foreignId, CompanyId = Guid.NewGuid(), Code = "FOREIGN", Name = "Other company" });
        db.SaveChanges(true);
        return db;
    }
    private static ShiftsController Controller(ApplicationDbContext db, Guid userId) =>
        new(new CSM.Infrastructure.Services.ShiftService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) } } };
    [Fact]
    public void Supervisor_with_submit_permission_still_has_no_shift_management_controls()
    {
        using var context = Context(out var state);
        state.Role = "Supervisor";
        var page = Render(context, "list");
        page.WaitForAssertion(() => Assert.Contains("Day shift", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Add Shift") || b.TextContent.Contains("Deactivate") || b.TextContent.Trim() == "Edit");
    }

    private static IRenderedFragment Render(TestContext context, string kind) => kind switch {
        "list" => context.RenderComponent<Shifts>(),
        "create" => context.RenderComponent<ShiftCreate>(),
        _ => context.RenderComponent<ShiftEdit>(p => p.Add(x => x.ShiftId, Id))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new State();
        var api = DispatchProxy.Create<IShiftService, Api>(); ((Api)(object)api).State = state;
        var auth = DispatchProxy.Create<IAuthService, Auth>(); ((Auth)(object)auth).State = state;
        context.Services.AddSingleton(api); context.Services.AddSingleton(auth); return context;
    }
    public sealed class State { public string Role = "CompanyAdmin"; public HttpStatusCode? Status; public bool CanSubmit = true; public bool Missing; public bool ActivationDenied; }
    private static ShiftResponse Record() => new() { Id = Id, Code = "DAY", Name = "Day shift", IsActive = true, StartTime = new(8, 0), EndTime = new(17, 0), StandardHours = 8m };
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IShiftService.GetAllAsync) => State.Status.HasValue
                ? Task.FromException<IReadOnlyCollection<ShiftResponse>>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<IReadOnlyCollection<ShiftResponse>>(new List<ShiftResponse> { Record() }),
            nameof(IShiftService.GetByIdAsync) => State.Status.HasValue
                ? Task.FromException<ShiftResponse?>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult<ShiftResponse?>(State.Missing ? null : Record()),
            nameof(IShiftService.ChangeActiveStatusAsync) when State.ActivationDenied =>
                Task.FromException<ShiftResponse>(new HttpRequestException("private diagnostic", null, HttpStatusCode.Forbidden)),
            _ => throw new InvalidOperationException("Unexpected shift mutation.")
        };
    }
    public class Auth : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(method!.Name == nameof(IAuthService.GetRolesAsync) ? new[] { State.Role } : State.CanSubmit ? new[] { "HRM_Shift_View", "HRM_Shift_Submit" } : new[] { "HRM_Shift_View" });
    }
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Response(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private diagnostic") });
    }
}
