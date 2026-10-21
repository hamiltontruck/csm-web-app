using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InventorySiteAccessTests
{
    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.ProcurementOfficer)]
    public async Task Inventory_user_can_read_only_active_assignments_in_own_company(string role)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid();
        var actor = new User { Id = Guid.NewGuid(), CompanyId = company, IsActive = true,
            UserRoles = [new UserRole { Id = Guid.NewGuid(),
                Role = new Role { Id = Guid.NewGuid(), Name = role } }] };
        var assigned = Site(company, "Assigned");
        var unassigned = Site(company, "Unassigned");
        var inactiveAccess = Site(company, "Inactive access");
        var foreign = Site(Guid.NewGuid(), "Foreign company");
        db.Users.Add(actor);
        db.ConstructionSites.AddRange(assigned, unassigned, inactiveAccess, foreign);
        db.UserSiteAccesses.AddRange(
            Access(actor, assigned, true), Access(actor, inactiveAccess, false),
            // A corrupt cross-company assignment must not grant access.
            Access(actor, foreign, true));
        db.SaveChanges(acceptAllChangesOnSuccess: true);

        var service = new SiteService(db);
        Assert.Equal(assigned.Id, Assert.Single(await service.GetAllAsync(actor.Id)).Id);
        Assert.Equal(assigned.Id, (await service.GetByIdAsync(actor.Id, assigned.Id)).Id);
        foreach (var denied in new[] { unassigned, inactiveAccess, foreign })
            await Assert.ThrowsAsync<SiteManagementException>(() => service.GetByIdAsync(actor.Id, denied.Id));
        await Assert.ThrowsAsync<SiteManagementException>(() =>
            service.SetActiveStatusAsync(actor.Id, assigned.Id, false));
        await Assert.ThrowsAsync<SiteManagementException>(() =>
            service.GetAssignmentOptionsAsync(actor.Id));
        Assert.True(assigned.IsActive);
    }

    private static ConstructionSite Site(Guid company, string name) =>
        new() { Id = Guid.NewGuid(), CompanyId = company, Name = name, SiteCode = name, IsActive = true };

    private static UserSiteAccess Access(User actor, ConstructionSite site, bool active) =>
        new() { Id = Guid.NewGuid(), UserId = actor.Id, User = actor,
            SiteId = site.Id, Site = site, CompanyId = site.CompanyId, IsActive = active };
}
