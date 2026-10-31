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

public sealed class EquipmentAssignmentFeedbackTests
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
    public async Task Assignment_controller_matches_the_service_role_contract(string role, bool expected)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options => options.AddPolicy(AppPolicies.ProcurementManagement,
            policy => policy.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(EquipmentAssignmentsController).GetCustomAttributes(true).OfType<IAuthorizeData>());
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!);
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Supervisor_receives_403_for_read_create_and_release_without_creating_records()
    {
        using var db = Database(AppRoles.Supervisor, out var userId);
        var controller = Controller(db, userId);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetAll(null, null, null, null, default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetById(Guid.NewGuid(), default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Create(
            new(Guid.NewGuid(), SiteId, null, DateTime.UtcNow, null, null), default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Release(Guid.NewGuid(),
            new CSM.Api.Controllers.ReleaseEquipmentAssignmentRequest(DateTime.UtcNow, null), default)).Result).StatusCode);
        Assert.Empty(db.EquipmentAssignments);
        Assert.Empty(db.Equipment);
    }

    [Fact]
    public async Task Assignment_validation_keeps_HTTP_400()
    {
        using var db = Database(AppRoles.SiteManager, out var userId);
        var result = await Controller(db, userId).Create(
            new(Guid.Empty, SiteId, null, default, null, null), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view equipment assignments.", false)]
    [InlineData(500, "Unable to load equipment assignments. Please try again.", true)]
    public void Assignment_list_handles_errors_without_creation_or_release_controls(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentAssignments>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-equipment-assignments").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-assignments-sign-in").Count == 1);
        Assert.DoesNotContain("New Assignment", page.Markup);
        Assert.DoesNotContain("No equipment assignments found.", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Empty(page.FindAll(".spinner-border, .modal"));
        Assert.Contains("Back", page.Markup);
    }

    [Fact]
    public void Assignment_list_can_retry_and_keeps_only_the_selected_site()
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<EquipmentAssignments>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find("#retry-equipment-assignments").Click();
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.Contains("New Assignment", page.Markup);
        Assert.Contains("Release", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to create equipment assignments.", false)]
    [InlineData(500, "Unable to load assignment form. Please try again.", true)]
    public void Direct_assignment_create_route_handles_errors_without_rendering_form(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentAssignmentCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        Assert.Equal(retry, page.FindAll("#retry-equipment-assignment-create").Count == 1);
        Assert.Contains("Back", page.Markup);
        if (status == 401)
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString($"/equipment/assignments/create/{SiteId}"),
                page.Find("#equipment-assignment-create-sign-in").GetAttribute("href"));
    }

    [Fact]
    public void Allowed_user_can_open_assignment_form()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<EquipmentAssignmentCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("Save Assignment", page.Markup);
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

    private static EquipmentAssignmentsController Controller(ApplicationDbContext db, Guid userId) =>
        new(new EquipmentAssignmentService(db)) { ControllerContext = new ControllerContext {
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
            nameof(IEquipmentService.GetAssignmentsAsync) => Status.HasValue
                ? Task.FromException<List<EquipmentAssignmentResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(new List<EquipmentAssignmentResponse> {
                    new() { Id = Guid.NewGuid(), ConstructionSiteId = SiteId, EquipmentName = "Excavator",
                        ConstructionSiteName = "Hamilton Test Site", AssignmentNumber = "EA-001", AssignedAtUtc = DateTime.UtcNow },
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
