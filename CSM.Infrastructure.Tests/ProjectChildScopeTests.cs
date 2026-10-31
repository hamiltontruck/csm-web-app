using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Constants;
using CSM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ProjectChildScopeTests
{
    [Fact]
    public async Task Empty_actor_cannot_read_project_children()
    {
        using var db = Db();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).GetAllAsync(Guid.Empty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new ProjectDelayService(db).GetAllAsync(Guid.Empty, null, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new ProjectMilestoneService(db).GetAllAsync(Guid.Empty, Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new DailySiteDiaryService(db).GetAllAsync(Guid.Empty, null, null));
    }
    [Fact]
    public async Task Company_admin_reads_only_own_company_children_and_revocation_is_live()
    {
        using var db = Db();
        var company = Guid.NewGuid();
        var actor = new User { IsActive = true, CompanyId = company, UserRoles = [new() { Role = new Role { Name = AppRoles.CompanyAdmin, GranularPermissions = [new() { Permission = GranularPermission.Projects_View }] } }] };
        var own = new Project { CompanyId = company }; var foreign = new Project { CompanyId = Guid.NewGuid() };
        db.Users.Add(actor); db.Projects.AddRange(own, foreign);
        db.WbsTasks.AddRange(new WbsTask { CompanyId = company, ProjectId = own.Id }, new WbsTask { CompanyId = foreign.CompanyId, ProjectId = foreign.Id });
        await db.SaveChangesAsync(true);
        Assert.Single(await new WbsTaskService(db).GetAllAsync(actor.Id));
        actor.UserRoles.First().Role.GranularPermissions.Clear(); await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).GetAllAsync(actor.Id));
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
