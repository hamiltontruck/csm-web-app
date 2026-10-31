using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ProjectChildHistoryTests
{
    [Theory]
    [InlineData(AppRoles.CompanyAdmin)]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Supervisor)]
    public async Task Permitted_inactive_site_history_remains_readable_but_revoked_assignment_does_not(string role)
    {
        using var db = ProjectChildScopeTests.Db();
        var f = ProjectChildScopeTests.Seed(db, role);
        var assignment = new UserSiteAccess { UserId = f.Actor.Id, CompanyId = f.Project.CompanyId, SiteId = f.Project.ConstructionSiteId, IsActive = true };
        db.UserSiteAccesses.Add(assignment);
        var site = await db.ConstructionSites.SingleAsync(x => x.Id == f.Project.ConstructionSiteId);
        site.IsActive = false;
        await db.SaveChangesAsync(true);
        Assert.Single((await new WbsTaskService(db).GetAllAsync(f.Actor.Id)).Where(x => x.ProjectId == f.Project.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).CreateAsync(f.Actor.Id, new() { ProjectId = f.Project.Id }));
        if (role == AppRoles.Supervisor)
        {
            assignment.IsActive = false; await db.SaveChangesAsync(true);
            Assert.Empty(await new WbsTaskService(db).GetAllAsync(f.Actor.Id));
        }
    }
}
