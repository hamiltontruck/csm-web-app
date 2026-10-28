using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class HrSiteAssignmentAccessTests
{
    [Theory]
    [InlineData("SuperAdmin", true, true)]
    [InlineData("CompanyAdmin", true, true)]
    [InlineData("SiteManager", true, false)]
    [InlineData("Supervisor", true, false)]
    [InlineData("HrOfficer", true, true)]
    [InlineData("HROfficer", true, true)]
    [InlineData("Administrator", false, false)]
    [InlineData("Storekeeper", false, false)]
    [InlineData("Employee", false, false)]
    public Task Endpoints_match_existing_service_roles_and_keep_granular_policies(string role, bool read, bool manage) =>
        Authorize(role, true, true, read, manage);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public Task Company_admin_still_requires_each_granular_permission(bool view, bool manage) =>
        Authorize("CompanyAdmin", view, manage, view, manage);

    private static async Task Authorize(string role, bool viewPermission, bool managePermission, bool canRead, bool canManage)
    {
        using var db = Database(role, out var user, out _, out _);
        var roleId = db.UserRoles.Single(x => x.UserId == user).RoleId;
        if (viewPermission) db.Set<RoleGranularPermission>().Add(new() { RoleId = roleId, Permission = GranularPermission.HRM_SiteAssignment_View });
        if (managePermission) db.Set<RoleGranularPermission>().Add(new() { RoleId = roleId, Permission = GranularPermission.HRM_SiteAssignment_Manage });
        db.SaveChanges(true);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(db);
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options => {
            options.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles));
            options.AddPolicy("HRM.SiteAssignment.View", p => p.RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(GranularPermission.HRM_SiteAssignment_View.ToString())));
            options.AddPolicy("HRM.SiteAssignment.Manage", p => p.RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(GranularPermission.HRM_SiteAssignment_Manage.ToString())));
        });
        using var provider = services.BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var name in new[] { "GetAll", "GetById", "Create", "Update" })
        {
            var methodMetadata = typeof(SiteAssignmentsController).GetMethod(name)!.GetCustomAttributes(true).OfType<IAuthorizeData>().ToList();
            var expectedPolicy = name is "Create" or "Update" ? "HRM.SiteAssignment.Manage" : "HRM.SiteAssignment.View";
            Assert.Contains(methodMetadata, x => x.Policy == expectedPolicy);
            var metadata = typeof(SiteAssignmentsController).GetCustomAttributes(true).OfType<IAuthorizeData>().Concat(methodMetadata);
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(name is "Create" or "Update" ? canManage : canRead,
                (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
        }
    }

    [Fact]
    public async Task Company_admin_reads_own_company_and_foreign_missing_and_invalid_dates_keep_correct_statuses()
    {
        using var db = Database("CompanyAdmin", out var actor, out var own, out var foreign);
        var controller = Controller(db, actor);
        var response = await controller.GetAll(null, null, default);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<CSM.Application.HRM.SiteAssignments.Dtos.SiteAssignmentResponse>>(
            Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(own, Assert.Single(rows).Id);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetById(foreign, default)).Result).StatusCode);
        Assert.IsType<NotFoundObjectResult>((await controller.GetById(Guid.NewGuid(), default)).Result);
        var row = db.SiteAssignments.Single(x => x.Id == own);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(new(row.EmployeeId, row.ConstructionSiteId,
            new(2026,10,29), new(2026,10,28), null, false, null), default)).Result);
        Assert.Equal(2, db.SiteAssignments.Count());
    }

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("SiteManager")]
    public async Task Read_only_roles_cannot_create_or_update(string role)
    {
        using var db = Database(role, out var actor, out var own, out _);
        var controller = Controller(db, actor);
        var row = db.SiteAssignments.Single(x => x.Id == own);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Create(new(row.EmployeeId, row.ConstructionSiteId,
            new(2026,10,28), null, null, false, null), default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Update(own,
            new(new(2026,10,28), null, null, false, null), default)).Result).StatusCode);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Company_admin_needs_active_account_and_assigned_company(bool assigned, bool active)
    {
        using var db = Database("CompanyAdmin", out var actor, out _, out _);
        var user = db.Users.Single(x => x.Id == actor);
        if (!assigned) user.CompanyId = null;
        user.IsActive = active;
        db.SaveChanges(true);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await Controller(db, actor).GetAll(null, null, default)).Result).StatusCode);
    }

    private static ApplicationDbContext Database(string role, out Guid actor, out Guid own, out Guid foreign)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        actor = Guid.NewGuid(); var company = Guid.NewGuid(); own = Guid.NewGuid(); foreign = Guid.NewGuid();
        db.Users.Add(new User { Id = actor, CompanyId = company, IsActive = true, UserRoles = [
            new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        foreach (var pair in new[] { (own, company), (foreign, Guid.NewGuid()) })
            db.SiteAssignments.Add(new SiteAssignment { Id = pair.Item1, CompanyId = pair.Item2, StartDate = new(2026,10,28),
                Employee = new Employee { Id = Guid.NewGuid(), CompanyId = pair.Item2, EmployeeNumber = pair.Item1.ToString(), FirstName = "Test", LastName = "Employee", Status = EmployeeStatus.Active },
                ConstructionSite = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = pair.Item2, SiteCode = pair.Item1.ToString(), Name = "Test Site" } });
        db.SaveChanges(true); return db;
    }
    private static SiteAssignmentsController Controller(ApplicationDbContext db, Guid actor) =>
        new(new CSM.Infrastructure.Services.SiteAssignmentService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test")) } } };
}
