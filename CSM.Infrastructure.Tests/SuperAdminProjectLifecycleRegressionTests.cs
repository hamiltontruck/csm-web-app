using CSM.Application.Projects.WbsTasks.Dtos;
using CSM.Domain.Constants;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

/// <summary>
/// Read/create and live-grant project-boundary checks against an isolated in-memory fixture.
/// Production databases and deployments are not used.
/// </summary>
public sealed class SuperAdminProjectLifecycleRegressionTests
{
    [Fact]
    public async Task Companyless_superadmin_creates_and_reads_project_child_in_resource_company()
    {
        using var db = ProjectChildScopeTests.Db();
        var fixture = ProjectChildScopeTests.Seed(db, AppRoles.SuperAdmin);
        fixture.Actor.CompanyId = null;
        await db.SaveChangesAsync();

        var service = new WbsTaskService(db);
        var created = await service.CreateAsync(fixture.Actor.Id, new CreateWbsTaskRequest
        {
            ProjectId = fixture.ForeignTask.ProjectId,
            TaskCode = "REGRESSION-WBS",
            Name = "Fixture project task"
        });
        var fetched = await service.GetByIdAsync(fixture.Actor.Id, created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(fixture.ForeignTask.CompanyId, fetched!.CompanyId);
        Assert.Equal(fixture.ForeignTask.ProjectId, fetched.ProjectId);
        Assert.Equal("Fixture project task", fetched.Name);
        Assert.Equal(3, await db.WbsTasks.CountAsync());
        Assert.Null(fixture.Actor.CompanyId);
    }

    [Fact]
    public async Task Revoked_global_project_grant_denies_new_create_and_preserves_saved_records()
    {
        using var db = ProjectChildScopeTests.Db();
        var fixture = ProjectChildScopeTests.Seed(db, AppRoles.SuperAdmin);
        fixture.Actor.CompanyId = null;
        await db.SaveChangesAsync();

        var service = new WbsTaskService(db);
        await service.CreateAsync(fixture.Actor.Id, new CreateWbsTaskRequest
        {
            ProjectId = fixture.ForeignTask.ProjectId,
            TaskCode = "AUTHORIZED",
            Name = "First task"
        });
        var countBefore = await db.WbsTasks.CountAsync();

        fixture.Actor.UserRoles.First().Role.GranularPermissions.Clear();
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(fixture.Actor.Id, new CreateWbsTaskRequest
            {
                ProjectId = fixture.ForeignTask.ProjectId,
                TaskCode = "FORBIDDEN",
                Name = "Should not be saved"
            }));
        Assert.Equal(countBefore, await db.WbsTasks.CountAsync());
        Assert.DoesNotContain(db.WbsTasks, task => task.TaskCode == "FORBIDDEN");
        Assert.Null(fixture.Actor.CompanyId);
    }
}
