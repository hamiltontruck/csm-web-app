using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Equipment;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentMaintenanceFeedbackTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");
    private static readonly Guid ActiveEquipmentId = Guid.NewGuid();
    private static readonly Guid OtherEquipmentId = Guid.NewGuid();
    private static readonly Guid ReleasedEquipmentId = Guid.NewGuid();

    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.CompanyAdmin, true)]
    [InlineData(AppRoles.SiteManager, true)]
    [InlineData(AppRoles.Supervisor, false)]
    [InlineData(AppRoles.Storekeeper, false)]
    [InlineData(AppRoles.ProcurementOfficer, false)]
    [InlineData(AppRoles.Administrator, false)]
    public async Task Maintenance_controller_matches_the_service_role_contract(string role, bool expected)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options => options.AddPolicy(AppPolicies.ProcurementManagement,
            policy => policy.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(EquipmentMaintenanceController).GetCustomAttributes(true).OfType<IAuthorizeData>());
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!);
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Supervisor_receives_403_for_read_and_create_without_creating_records()
    {
        using var db = Database(AppRoles.Supervisor, out var userId);
        var controller = Controller(db, userId);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetAll(null, null, default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetById(Guid.NewGuid(), default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Create(
            new(Guid.NewGuid(), "Test", DateTime.UtcNow, null, null, "ETB", null, null, null, null, null), default)).Result).StatusCode);
        Assert.Empty(db.EquipmentMaintenanceRecords);
        Assert.Empty(db.Equipment);
    }

    [Fact]
    public async Task Maintenance_validation_keeps_HTTP_400()
    {
        using var db = Database(AppRoles.SiteManager, out var userId);
        var result = await Controller(db, userId).Create(
            new(Guid.Empty, "", default, null, null, "ETB", null, null, null, null, null), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view equipment maintenance.", false)]
    [InlineData(500, "Unable to load equipment maintenance. Please try again.", true)]
    public void Maintenance_list_shows_safe_feedback_without_creation_controls(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentMaintenance>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-equipment-maintenance").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-maintenance-sign-in").Count == 1);
        Assert.DoesNotContain("New Maintenance", page.Markup);
        Assert.DoesNotContain("No maintenance records found", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Empty(page.FindAll(".spinner-border"));
        Assert.Contains("Back", page.Markup);
    }

    [Fact]
    public void Maintenance_retry_preserves_active_assignment_site_filter()
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<EquipmentMaintenance>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find("#retry-equipment-maintenance").Click();
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.DoesNotContain("Released equipment", page.Markup);
        Assert.Contains("New Maintenance", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to create equipment maintenance.", false)]
    [InlineData(500, "Unable to load maintenance form. Please try again.", true)]
    public void Direct_maintenance_create_route_checks_access_before_showing_form(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentMaintenanceCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        Assert.Equal(retry, page.FindAll("#retry-equipment-maintenance-create").Count == 1);
        Assert.Contains("Back", page.Markup);
        if (status == 401)
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString($"/equipment/maintenance/create/{SiteId}"),
                page.Find("#equipment-maintenance-create-sign-in").GetAttribute("href"));
    }

    [Fact]
    public void Allowed_user_can_open_maintenance_form_with_site_equipment()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<EquipmentMaintenanceCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("Save Maintenance", page.Markup);
        Assert.Contains("Excavator", page.Markup);
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    private static ApplicationDbContext Database(string role, out Guid userId)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, IsActive = true, CompanyId = Guid.NewGuid(),
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        db.SaveChanges(true);
        return db;
    }

    private static EquipmentMaintenanceController Controller(ApplicationDbContext db, Guid userId) =>
        new(new EquipmentMaintenanceService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) } } };

    private static TestContext Context(out Api api)
    {
        var context = new TestContext();
        var service = DispatchProxy.Create<IEquipmentService, Api>();
        api = (Api)(object)service;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        return context;
    }

    public class Api : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IEquipmentService.GetMaintenanceAsync) => Status.HasValue
                ? Task.FromException<List<EquipmentMaintenanceResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(new List<EquipmentMaintenanceResponse> {
                    new() { EquipmentId = ActiveEquipmentId, EquipmentName = "Excavator", MaintenanceType = "Preventive", ScheduledAtUtc = DateTime.UtcNow },
                    new() { EquipmentId = OtherEquipmentId, EquipmentName = "Other site equipment", ScheduledAtUtc = DateTime.UtcNow },
                    new() { EquipmentId = ReleasedEquipmentId, EquipmentName = "Released equipment", ScheduledAtUtc = DateTime.UtcNow }
                }),
            nameof(IEquipmentService.GetAssignmentsAsync) => Task.FromResult(new List<EquipmentAssignmentResponse> {
                new() { EquipmentId = ActiveEquipmentId, ConstructionSiteId = SiteId },
                new() { EquipmentId = OtherEquipmentId, ConstructionSiteId = Guid.NewGuid() },
                new() { EquipmentId = ReleasedEquipmentId, ConstructionSiteId = SiteId, ReleasedAtUtc = DateTime.UtcNow }
            }),
            nameof(IEquipmentService.GetEquipmentAsync) => Task.FromResult(new List<EquipmentResponse> {
                new() { Id = ActiveEquipmentId, Name = "Excavator", Status = "Available" },
                new() { Id = OtherEquipmentId, Name = "Other site equipment", Status = "Available" },
                new() { Id = ReleasedEquipmentId, Name = "Released equipment", Status = "Available" }
            }),
            _ => throw new InvalidOperationException("Unexpected equipment mutation.")
        };
    }

    public class Sites : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }
}
