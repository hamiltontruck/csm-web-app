using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Application.Projects.WbsTasks.Dtos;
using CSM.Application.Projects.DailySiteDiaries.Dtos;
using CSM.Application.Projects.DailyProgressTaskEntries.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ProjectChildScopeTests
{
    [Theory]
    [InlineData("wbs")][InlineData("delays")][InlineData("milestones")][InlineData("diaries")]
    [InlineData("resources")][InlineData("entries")][InlineData("photos")]
    public async Task Empty_actor_cannot_read_project_children(string service)
    {
        using var db = Db();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
            switch (service) {
                case "wbs": await new WbsTaskService(db).GetAllAsync(Guid.Empty); break;
                case "delays": await new ProjectDelayService(db).GetAllAsync(Guid.Empty, null, null); break;
                case "milestones": await new ProjectMilestoneService(db).GetAllAsync(Guid.Empty, Guid.NewGuid()); break;
                case "diaries": await new DailySiteDiaryService(db).GetAllAsync(Guid.Empty, null, null); break;
                case "resources": await new TaskResourceAssignmentService(db).GetByTaskAsync(Guid.NewGuid(), Guid.Empty); break;
                case "entries": await new DailyProgressTaskEntryService(db).GetByLogAsync(Guid.Empty, Guid.NewGuid()); break;
                case "photos": await new DailyProgressPhotoService(db, new PhotoEnvironment()).GetByDiaryAsync(Guid.Empty, Guid.NewGuid()); break;
            }
        });
    }
    [Fact]
    public async Task Company_admin_reads_only_own_company_children_and_revocation_is_live()
    {
        using var db = Db(); var f = Seed(db, AppRoles.CompanyAdmin);
        Assert.Single(await new WbsTaskService(db).GetAllAsync(f.Actor.Id));
        Assert.Null(await new WbsTaskService(db).GetByIdAsync(f.Actor.Id, f.ForeignTask.Id));
        f.Actor.UserRoles.First().Role.GranularPermissions.Clear(); await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).GetAllAsync(f.Actor.Id));
    }
    [Fact]
    public async Task Supervisor_is_limited_to_active_assignments_and_cannot_manage_structure()
    {
        using var db = Db(); var f = Seed(db, AppRoles.Supervisor);
        Assert.Empty(await new WbsTaskService(db).GetAllAsync(f.Actor.Id));
        var assignment = new UserSiteAccess { UserId = f.Actor.Id, CompanyId = f.Project.CompanyId, SiteId = f.Project.ConstructionSiteId, IsActive = true };
        db.UserSiteAccesses.Add(assignment); await db.SaveChangesAsync(true);
        Assert.Single(await new WbsTaskService(db).GetAllAsync(f.Actor.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).CreateAsync(f.Actor.Id, new() { ProjectId = f.Project.Id }));
        var diary = await new DailySiteDiaryService(db).CreateAsync(f.Actor.Id, new() { SiteId = f.Project.ConstructionSiteId });
        Assert.Equal(f.Project.CompanyId, diary.TenantId); Assert.Null(diary.ProjectId);
        assignment.IsActive = false; await db.SaveChangesAsync(true);
        Assert.Empty(await new WbsTaskService(db).GetAllAsync(f.Actor.Id));
        Assert.Null(await new DailySiteDiaryService(db).GetByIdAsync(f.Actor.Id, diary.Id));
    }
    [Fact]
    public async Task Companyless_super_admin_has_global_scope_with_real_grants()
    {
        using var db = Db(); var f = Seed(db, AppRoles.SuperAdmin); f.Actor.CompanyId = null; await db.SaveChangesAsync(true);
        Assert.Equal(2, (await new WbsTaskService(db).GetAllAsync(f.Actor.Id)).Count);
        var task = await new WbsTaskService(db).CreateAsync(f.Actor.Id, new() { ProjectId = f.ForeignTask.ProjectId });
        Assert.Equal(f.ForeignTask.CompanyId, task.CompanyId);
    }
    [Fact]
    public async Task Foreign_parent_and_mismatched_diary_site_writes_leave_state_unchanged()
    {
        using var db = Db(); var f = Seed(db, AppRoles.CompanyAdmin);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WbsTaskService(db).CreateAsync(f.Actor.Id, new() { ProjectId = f.ForeignTask.ProjectId }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new DailySiteDiaryService(db).CreateAsync(f.Actor.Id, new() { ProjectId = f.Project.Id, SiteId = f.ForeignProject.ConstructionSiteId }));
        Assert.Equal(2, await db.WbsTasks.CountAsync()); Assert.Empty(db.DailySiteDiaries);
        var milestone = new Milestone { CompanyId = f.ForeignTask.CompanyId, ProjectId = f.ForeignTask.ProjectId }; db.Milestones.Add(milestone); await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<ArgumentException>(() => new WbsTaskService(db).CreateAsync(f.Actor.Id, new() { ProjectId = f.Project.Id, MilestoneId = milestone.Id }));
        Assert.Equal(2, await db.WbsTasks.CountAsync());
    }
    [Fact]
    public async Task Task_entries_reject_foreign_tasks_and_preserve_progress_calculations()
    {
        using var db = Db(); var f = Seed(db, AppRoles.CompanyAdmin);
        var log = new DailyProgressLog { CompanyId = f.Project.CompanyId, ProjectId = f.Project.Id };
        var phase = new ProjectPhase { CompanyId = f.Project.CompanyId, ProjectId = f.Project.Id };
        var task = new WorkTask { CompanyId = f.Project.CompanyId, ProjectId = f.Project.Id, ProjectPhase = phase };
        var sibling = new WorkTask { CompanyId = f.Project.CompanyId, ProjectId = f.Project.Id, ProjectPhase = phase, ProgressPercentage = 20 };
        var foreign = new WorkTask { CompanyId = f.ForeignProject.CompanyId, ProjectId = f.ForeignProject.Id };
        db.DailyProgressLogs.Add(log); db.WorkTasks.AddRange(task, sibling, foreign); await db.SaveChangesAsync(true);
        var service = new DailyProgressTaskEntryService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(f.Actor.Id, new(log.Id, foreign.Id, 100, 1, 0, "invalid", null)));
        Assert.Equal(0, foreign.ProgressPercentage); Assert.Empty(db.DailyProgressTaskEntries);
        await service.CreateAsync(f.Actor.Id, new(log.Id, task.Id, 60, 3, 2, "work", null));
        Assert.Equal(60, task.ProgressPercentage); Assert.Equal(40, phase.ProgressPercentage);
        Assert.Equal(WorkTaskStatus.InProgress, task.Status); Assert.Single(await service.GetByLogAsync(f.Actor.Id, log.Id));
    }
    [Fact]
    public async Task Photos_authorize_before_IO_and_private_downloads_retain_legacy_support()
    {
        using var db = Db(); var f = Seed(db, AppRoles.CompanyAdmin); var env = new PhotoEnvironment();
        try {
            var foreign = new DailySiteDiary { CompanyId = f.ForeignProject.CompanyId, SiteId = f.ForeignProject.ConstructionSiteId, ProjectId = f.ForeignProject.Id };
            var own = new DailySiteDiary { CompanyId = f.Project.CompanyId, SiteId = f.Project.ConstructionSiteId, ProjectId = f.Project.Id };
            db.DailySiteDiaries.AddRange(own, foreign); await db.SaveChangesAsync(true);
            var service = new DailyProgressPhotoService(db, env);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(f.Actor.Id, foreign.Id, new MemoryStream([1]), "x.jpg", null));
            Assert.False(Directory.Exists(env.ContentRootPath)); Assert.Empty(db.DailyProgressPhotos);
            var response = await service.UploadAsync(f.Actor.Id, own.Id, new MemoryStream([1,2,3]), "x.jpg", null);
            Assert.Equal($"/api/daily-progress-photos/{response.Id}/content", response.FilePath);
            Assert.False(Directory.Exists(env.WebRootPath));
            var content = await service.DownloadAsync(f.Actor.Id, response.Id); Assert.NotNull(content); using (content.Content) { Assert.Equal(3, content.Content.Length); }
            var legacyName = Guid.NewGuid()+".jpg"; Directory.CreateDirectory(Path.Combine(env.WebRootPath, "uploads", "daily-progress"));
            await File.WriteAllBytesAsync(Path.Combine(env.WebRootPath, "uploads", "daily-progress", legacyName), [9]);
            var legacy = new DailyProgressPhoto { CompanyId = own.CompanyId, DailySiteDiaryId = own.Id, FileName = legacyName, FilePath = "/uploads/daily-progress/"+legacyName };
            db.DailyProgressPhotos.Add(legacy); await db.SaveChangesAsync(true);
            var download = await service.DownloadAsync(f.Actor.Id, legacy.Id); Assert.NotNull(download); download.Content.Dispose();
            f.Actor.CompanyId = f.ForeignProject.CompanyId; await db.SaveChangesAsync(true);
            Assert.Null(await service.DownloadAsync(f.Actor.Id, response.Id));
            await service.DeleteAsync(f.Actor.Id, response.Id); Assert.False(db.DailyProgressPhotos.Single(x => x.Id == response.Id).IsDeleted);
        } finally { if (Directory.Exists(env.ContentRootPath)) Directory.Delete(env.ContentRootPath, true); }
    }
    internal static ApplicationDbContext Db() => new TestDb();
    private sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) {}
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
    internal static Fixture Seed(ApplicationDbContext db, string role)
    {
        var company = Guid.NewGuid(); var foreignCompany = Guid.NewGuid();
        var actor = new User { IsActive = true, CompanyId = company, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() } }] };
        var site = new ConstructionSite { CompanyId = company, IsActive = true }; var foreignSite = new ConstructionSite { CompanyId = foreignCompany, IsActive = true };
        var own = new Project { CompanyId = company, ConstructionSiteId = site.Id }; var foreign = new Project { CompanyId = foreignCompany, ConstructionSiteId = foreignSite.Id };
        var ownTask = new WbsTask { CompanyId = company, ProjectId = own.Id }; var foreignTask = new WbsTask { CompanyId = foreignCompany, ProjectId = foreign.Id };
        db.Users.Add(actor); db.ConstructionSites.AddRange(site, foreignSite); db.Projects.AddRange(own, foreign); db.WbsTasks.AddRange(ownTask, foreignTask); db.SaveChanges(true);
        return new(actor, own, foreign, foreignTask);
    }
    internal record Fixture(User Actor, Project Project, Project ForeignProject, WbsTask ForeignTask);
    internal sealed class PhotoEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "project-photo-"+Guid.NewGuid());
        public string WebRootPath { get => Path.Combine(ContentRootPath, "wwwroot"); set {} }
        public string EnvironmentName { get; set; } = "Test"; public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
