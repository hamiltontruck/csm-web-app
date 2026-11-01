using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class LegacyBackendAuthorityFlowTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.CompanyAdmin)]
    [InlineData(AppRoles.SiteManager)]
    public async Task Rebar_managers_keep_valid_lifecycle_and_revocation_is_live(string roleName)
    {
        using var db = Db();
        var actor = Actor(roleName); var material = new Material { CompanyId = actor.CompanyId!.Value, MaterialType = MaterialType.RebarLinear };
        db.Users.Add(actor); db.Materials.Add(material); await db.SaveChangesAsync();
        var service = new RebarSpecService(db);
        var created = await service.CreateAsync(actor.Id, new() { MaterialId = material.Id, DiameterMm = 16, Grade = "B500" });
        Assert.Equal(16m, created.DiameterMm);
        Assert.Equal("B500", (await service.GetByMaterialIdAsync(actor.Id, material.Id)).Grade);
        await service.UpdateAsync(actor.Id, material.Id, new() { DiameterMm = 20, Grade = "B600" });
        Assert.Equal(20m, (await service.GetByMaterialIdAsync(actor.Id, material.Id)).DiameterMm);
        var grant = actor.UserRoles.Single().Role.GranularPermissions.Single(p => p.Permission == GranularPermission.Procurement_Inventory_Adjust);
        db.Remove(grant); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(actor.Id, material.Id, new() { DiameterMm = 30, Grade = "Denied" }));
        Assert.Equal(20m, (await service.GetByMaterialIdAsync(actor.Id, material.Id)).DiameterMm);
    }

    [Theory]
    [InlineData(AppRoles.CompanyAdmin)]
    [InlineData(AppRoles.SiteManager)]
    [InlineData(AppRoles.Supervisor)]
    public async Task Unowned_legacy_equipment_catalog_denies_tenant_roles(string roleName)
    {
        using var db = Db(); var actor = Actor(roleName); db.Users.Add(actor); await db.SaveChangesAsync();
        var service = new ProjectEquipmentService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync(actor.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(actor.Id, new() { EquipmentCode = "DENIED", Name = "Denied" }));
        Assert.Empty(db.ProjectEquipment);
    }

    [Fact]
    public async Task Global_catalog_super_admin_lifecycle_requires_current_grants_and_active_actor()
    {
        using var db = Db(); var actor = Actor(AppRoles.SuperAdmin); actor.CompanyId = null;
        db.Users.Add(actor); await db.SaveChangesAsync(); var service = new ProjectEquipmentService(db);
        var created = await service.CreateAsync(actor.Id, new() { EquipmentCode = "GLOBAL", Name = "Global equipment", Category = "Test" });
        Assert.Equal(created.Id, Assert.Single(await service.GetAllAsync(actor.Id)).Id);
        Assert.NotNull(await service.GetByIdAsync(actor.Id, created.Id));
        var grant = actor.UserRoles.Single().Role.GranularPermissions.Single(p => p.Permission == GranularPermission.Projects_Manage);
        db.Remove(grant); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(actor.Id, created.Id));
        Assert.Single(db.ProjectEquipment);
        actor.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync(actor.Id));
    }

    [Theory]
    [InlineData(AppRoles.CompanyAdmin)]
    [InlineData(AppRoles.SiteManager)]
    [InlineData(AppRoles.Supervisor)]
    public async Task Legacy_milestones_preserve_permitted_reads_and_reject_other_sites(string roleName)
    {
        using var db = Db(); var actor = Actor(roleName); var company = actor.CompanyId!.Value;
        var site = new ConstructionSite { CompanyId = company, IsActive = true };
        var otherSite = new ConstructionSite { CompanyId = company, IsActive = true };
        var project = new Project { CompanyId = company, ConstructionSiteId = site.Id };
        var other = new Project { CompanyId = company, ConstructionSiteId = otherSite.Id };
        var milestone = new Milestone { CompanyId = company, ProjectId = project.Id, Name = "Own", Status = MilestoneStatus.Pending };
        var assignment = new UserSiteAccess { UserId = actor.Id, CompanyId = company, SiteId = site.Id, IsActive = true };
        db.Users.Add(actor); db.ConstructionSites.AddRange(site, otherSite); db.Projects.AddRange(project, other);
        db.Milestones.Add(milestone); db.UserSiteAccesses.Add(assignment); await db.SaveChangesAsync();
        var service = new MilestoneService(db);
        Assert.Equal(milestone.Id, Assert.Single(await service.GetAllAsync(actor.Id, project.Id)).Id);
        Assert.Equal(milestone.Id, (await service.GetByIdAsync(actor.Id, milestone.Id)).Id);
        if (roleName != AppRoles.CompanyAdmin)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync(actor.Id, other.Id));
            assignment.IsActive = false; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(actor.Id, milestone.Id));
        }
        else
        {
            await service.CompleteAsync(actor.Id, milestone.Id); Assert.Equal(MilestoneStatus.Completed, milestone.Status);
            await service.ReopenAsync(actor.Id, milestone.Id); Assert.Equal(MilestoneStatus.Pending, milestone.Status);
        }
    }

    private static User Actor(string role) => new() { CompanyId = Guid.NewGuid(), IsActive = true,
        UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = Enum.GetValues<GranularPermission>()
            .Select(p => new RoleGranularPermission { Permission = p }).ToList() } }] };
    private static TestDb Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
