using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class LegacyBackendAuthorityTests
{
    [Theory]
    [InlineData(typeof(MilestonesController), "GetAll", AppRoles.Supervisor, true)]
    [InlineData(typeof(RebarSpecsController), "GetByMaterialId", AppRoles.SiteManager, true)]
    [InlineData(typeof(ProjectEquipmentController), "GetAll", AppRoles.CompanyAdmin, false)]
    public async Task Legacy_endpoint_role_ceiling_matches_service_authority(Type controller, string method, string role, bool expected)
        => Assert.Equal(expected, await Allowed(controller, method, role, Enum.GetValues<GranularPermission>()));
    [Theory]
    [InlineData(typeof(MilestonesController), "GetAll", GranularPermission.Projects_View)]
    [InlineData(typeof(MilestonesController), "Complete", GranularPermission.Projects_Manage)]
    [InlineData(typeof(RebarSpecsController), "Create", GranularPermission.Procurement_Inventory_Adjust)]
    [InlineData(typeof(ProjectEquipmentController), "GetAll", GranularPermission.Projects_View)]
    [InlineData(typeof(ProjectEquipmentController), "Create", GranularPermission.Projects_Manage)]
    public async Task Legacy_endpoints_require_actual_granular_grants(Type controller, string method, GranularPermission missing)
        => Assert.False(await Allowed(controller, method, AppRoles.SuperAdmin, Enum.GetValues<GranularPermission>().Where(p => p != missing)));
    [Fact]
    public async Task Legacy_milestone_service_denies_live_actor_without_project_view_grant()
    {
        using var db = Db(); var company = Guid.NewGuid();
        var actor = Actor(company); var project = new Project { CompanyId = company };
        db.Users.Add(actor); db.Projects.Add(project); await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new MilestoneService(db).GetAllAsync(actor.Id, project.Id));
    }
    [Fact]
    public async Task Rebar_service_denies_live_actor_without_inventory_view_grant()
    {
        using var db = Db(); var company = Guid.NewGuid(); var actor = Actor(company);
        var material = new Material { CompanyId = company, MaterialType = MaterialType.RebarLinear };
        db.Users.Add(actor); db.Materials.Add(material); db.RebarSpecs.Add(new RebarSpec { CompanyId = company, Material = material, Grade = "B500", DiameterMm = 16 }); await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new RebarSpecService(db).GetByMaterialIdAsync(actor.Id, material.Id));
    }
    private static User Actor(Guid company) => new() { CompanyId = company, IsActive = true, UserRoles = [new() { Role = new Role { Name = AppRoles.CompanyAdmin } }] };
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<bool> Allowed(Type controller, string method, string role, IEnumerable<GranularPermission> permissions)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddAuthorization(o => { o.AddPolicy(AppPolicies.ProjectManagement, p => p.RequireRole(AppPolicies.ProjectManagementRoles)); o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)); });
        using var provider = services.AddGrantedBackendPermissions(role, permissions).BuildServiceProvider();
        var data = controller.GetCustomAttributes(true).OfType<IAuthorizeData>().Concat(controller.GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        var actor = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test")));
        return (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded;
    }
}
