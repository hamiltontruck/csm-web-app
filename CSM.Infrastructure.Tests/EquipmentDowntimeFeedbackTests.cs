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

public sealed class EquipmentDowntimeFeedbackTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");

    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.CompanyAdmin, true)]
    [InlineData(AppRoles.SiteManager, true)]
    [InlineData(AppRoles.Supervisor, false)]
    [InlineData(AppRoles.Storekeeper, false)]
    [InlineData(AppRoles.ProcurementOfficer, false)]
    [InlineData(AppRoles.Administrator, false)]
    public async Task Downtime_controller_matches_the_service_role_contract(string role, bool expected)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options => options.AddPolicy(AppPolicies.ProcurementManagement,
            policy => policy.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(EquipmentDowntimeController).GetCustomAttributes(true).OfType<IAuthorizeData>());
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!);
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Supervisor_receives_403_for_read_create_and_close_without_creating_records()
    {
        using var db = Database(AppRoles.Supervisor, out var userId);
        var controller = Controller(db, userId);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetAll()).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetById(Guid.NewGuid())).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Create(
            new(Guid.NewGuid(), SiteId, DateTime.UtcNow, null, "DT-001", "Test", null))).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Close(Guid.NewGuid(),
            new CSM.Api.Controllers.CloseEquipmentDowntimeRequest(DateTime.UtcNow, null))).Result).StatusCode);
        Assert.Empty(db.EquipmentDowntimeRecords);
        Assert.Empty(db.Equipment);
    }

    [Fact]
    public async Task Downtime_validation_and_missing_record_keep_400_and_404()
    {
        using var db = Database(AppRoles.SiteManager, out var userId);
        var controller = Controller(db, userId);
        var invalid = await controller.Create(new(Guid.Empty, SiteId, default, null, "", "", null));
        Assert.IsType<BadRequestObjectResult>(invalid.Result);
        Assert.IsType<NotFoundObjectResult>((await controller.GetById(Guid.NewGuid())).Result);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view equipment downtime.", false)]
    [InlineData(500, "Unable to load equipment downtime. Please try again.", true)]
    public void Downtime_list_shows_safe_feedback_without_mutation_controls(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentDowntime>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-equipment-downtime").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-downtime-sign-in").Count == 1);
        Assert.DoesNotContain("New Downtime", page.Markup);
        Assert.DoesNotContain("No equipment downtime records found", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Empty(page.FindAll(".spinner-border, .modal"));
        Assert.Contains("Back", page.Markup);
    }

    [Fact]
    public void Downtime_list_can_retry_and_filters_the_selected_site()
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<EquipmentDowntime>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find("#retry-equipment-downtime").Click();
        page.WaitForAssertion(() => Assert.Contains("DT-001", page.Markup));
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.Contains("New Downtime", page.Markup);
        Assert.Contains("Close", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to create equipment downtime.", false)]
    [InlineData(500, "Unable to load downtime form. Please try again.", true)]
    public void Direct_downtime_create_route_checks_access_before_showing_form(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentDowntimeCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        Assert.Equal(retry, page.FindAll("#retry-equipment-downtime-create").Count == 1);
        Assert.Contains("Back", page.Markup);
        if (status == 401)
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString($"/equipment/downtime/create/{SiteId}"),
                page.Find("#equipment-downtime-create-sign-in").GetAttribute("href"));
    }

    [Fact]
    public void Allowed_user_can_open_downtime_form()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<EquipmentDowntimeCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("Create Downtime", page.Markup);
        Assert.Contains("Excavator", page.Markup);
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

    private static EquipmentDowntimeController Controller(ApplicationDbContext db, Guid userId) =>
        new(new EquipmentDowntimeService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) } } };

    private static TestContext Context(out Api api)
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context);
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
            nameof(IEquipmentService.GetDowntimeAsync) => Status.HasValue
                ? Task.FromException<List<EquipmentDowntimeResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(new List<EquipmentDowntimeResponse> {
                    new() { Id = Guid.NewGuid(), ConstructionSiteId = SiteId, EquipmentName = "Excavator",
                        ConstructionSiteName = "Hamilton Test Site", DowntimeNumber = "DT-001", StartedAtUtc = DateTime.UtcNow },
                    new() { Id = Guid.NewGuid(), ConstructionSiteId = Guid.NewGuid(), EquipmentName = "Other site equipment" }
                }),
            nameof(IEquipmentService.GetEquipmentAsync) => Task.FromResult(new List<EquipmentResponse> {
                new() { Id = Guid.NewGuid(), Name = "Excavator", Status = "Available" }
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
