using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ProjectChildPostgresTests
{
    [Fact]
    public async Task PostgreSQL_translates_child_scope_and_authorized_writes()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "project_child_"+Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "OWN", Name = "Own" }; var foreign = new Company { Code = "OTHER", Name = "Other" };
            var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "OWN", Name = "Own" }; var elsewhere = new ConstructionSite { CompanyId = foreign.Id, SiteCode = "OTHER", Name = "Other" };
            var project = new Project { CompanyId = company.Id, ConstructionSiteId = site.Id, ProjectCode = "OWN", Name = "Own" }; var other = new Project { CompanyId = foreign.Id, ConstructionSiteId = elsewhere.Id, ProjectCode = "OTHER", Name = "Other" };
            var actor = new User { CompanyId = company.Id, Email = "manager@example.test", NormalizedEmail = "MANAGER@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = new Role { Name = "SiteManager", NormalizedName = "SITEMANAGER", GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() } }] };
            db.Companies.AddRange(company, foreign); db.ConstructionSites.AddRange(site, elsewhere); db.Projects.AddRange(project, other); db.Users.Add(actor);
            var assignment = new UserSiteAccess { UserId = actor.Id, CompanyId = company.Id, SiteId = site.Id, IsActive = true }; db.UserSiteAccesses.Add(assignment);
            db.WbsTasks.AddRange(new WbsTask { CompanyId = company.Id, ProjectId = project.Id, TaskCode = "OWN", Name = "Own" }, new WbsTask { CompanyId = foreign.Id, ProjectId = other.Id, TaskCode = "OTHER", Name = "Other" });
            await db.SaveChangesAsync();
            Assert.Single(await new WbsTaskService(db).GetAllAsync(actor.Id));
            Assert.Empty(await new ProjectDelayService(db).GetAllAsync(actor.Id, null, null));
            var delayDate = new DateOnly(2026, 10, 31);
            var delay = await new ProjectDelayService(db).CreateAsync(actor.Id, new() { ProjectId = project.Id, DelayDate = delayDate, Title = "Weather", Description = "Rain" });
            Assert.Equal(delayDate, Assert.Single(await new ProjectDelayService(db).GetAllAsync(actor.Id, null, null)).DelayDate);
            Assert.Equal(delayDate, (await new ProjectDelayService(db).GetByIdAsync(actor.Id, delay.Id))!.DelayDate);
            Assert.Empty(await new ProjectMilestoneService(db).GetAllAsync(actor.Id, project.Id));
            var diary = await new DailySiteDiaryService(db).CreateAsync(actor.Id, new() { SiteId = site.Id, WorkPerformed = "Authorized" });
            Assert.Equal(company.Id, diary.TenantId);
            Assert.Single(await new DailySiteDiaryService(db).GetAllAsync(actor.Id, null, null));
            var task = await new WbsTaskService(db).CreateAsync(actor.Id, new() { ProjectId = project.Id, TaskCode = "NEW", Name = "New" });
            Assert.Equal(company.Id, task.CompanyId);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).CreateAsync(actor.Id, new() { ProjectId = other.Id, TaskCode = "DENIED" }));
            var log = new DailyProgressLog { CompanyId = company.Id, ProjectId = project.Id, LogNumber = "LOG-1" };
            var work = new WorkTask { CompanyId = company.Id, ProjectId = project.Id, TaskNumber = "TASK-1", Title = "Work" };
            db.DailyProgressLogs.Add(log); db.WorkTasks.Add(work); await db.SaveChangesAsync();
            var entries = new DailyProgressTaskEntryService(db);
            var entry = await entries.CreateAsync(actor.Id, new(log.Id, work.Id, 25, 1, 0, "Work", null));
            Assert.Single(await entries.GetByLogAsync(actor.Id, log.Id));
            Assert.Empty(await entries.GetEquipmentAsync(actor.Id, entry.Id));
            Assert.Empty(await new TaskResourceAssignmentService(db).GetByTaskAsync(task.Id, actor.Id));
            var environment = new ProjectChildScopeTests.PhotoEnvironment();
            var photos = new DailyProgressPhotoService(db, environment);
            Assert.Empty(await photos.GetByDiaryAsync(actor.Id, diary.Id));
            Assert.Null(await photos.DownloadAsync(actor.Id, Guid.NewGuid()));
            assignment.IsActive = false; await db.SaveChangesAsync();
            Assert.Empty(await new WbsTaskService(db).GetAllAsync(actor.Id)); Assert.Empty(await new DailySiteDiaryService(db).GetAllAsync(actor.Id, null, null));
        } finally { await db.Database.EnsureDeletedAsync(); }
    }
}
