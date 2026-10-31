using CSM.Application.Features.Compliance.NonConformanceReports;
using CSM.Application.Features.Compliance.QualityChecklists;
using CSM.Application.Features.Compliance.SafetyObservations.DTOs;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Enums;
using CSM.Infrastructure.Features.Compliance.NonConformanceReports;
using CSM.Infrastructure.Features.Compliance.QualityChecklists;
using CSM.Infrastructure.Features.Compliance.SafetyObservations;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ComplianceParentOwnershipTests
{
    [Theory]
    [InlineData("site")]
    [InlineData("project")]
    [InlineData("project-site")]
    [InlineData("employee")]
    public async Task Observation_rejects_foreign_or_mismatched_parent_before_insert(string invalid)
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new SafetyObservationService(f.Db).CreateAsync(f.Actor.Id,
            new CreateSafetyObservationRequest { ConstructionSiteId = invalid == "site" ? f.ForeignSite.Id : f.Site.Id, ProjectId = invalid == "project" ? f.ForeignProject.Id : invalid == "project-site" ? f.OtherProject.Id : null, EmployeeId = invalid == "employee" ? f.ForeignEmployee.Id : null }));
        Assert.Empty(f.Db.SafetyObservations);
    }
    [Theory]
    [InlineData("site")]
    [InlineData("project")]
    [InlineData("project-site")]
    [InlineData("employee")]
    public async Task Ncr_rejects_foreign_or_mismatched_parent_before_insert(string invalid)
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new NonConformanceReportService(f.Db).CreateAsync(f.Actor.Id,
            new CreateNonConformanceReportRequest { ConstructionSiteId = invalid == "site" ? f.ForeignSite.Id : f.Site.Id, ProjectId = invalid == "project" ? f.ForeignProject.Id : invalid == "project-site" ? f.OtherProject.Id : null, AssignedToEmployeeId = invalid == "employee" ? f.ForeignEmployee.Id : null }));
        Assert.Empty(f.Db.NonConformanceReports);
    }
    [Fact]
    public async Task Checklist_rejects_foreign_site_before_insert()
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new QualityChecklistService(f.Db).CreateAsync(f.Actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = f.ForeignSite.Id }));
        Assert.Empty(f.Db.QualityChecklists);
    }
    [Fact]
    public async Task Own_company_records_can_be_created_and_read()
    {
        using var f = new Fixture();
        var observation = await new SafetyObservationService(f.Db).CreateAsync(f.Actor.Id, new CreateSafetyObservationRequest { ConstructionSiteId = f.Site.Id });
        var ncr = await new NonConformanceReportService(f.Db).CreateAsync(f.Actor.Id, new CreateNonConformanceReportRequest { ConstructionSiteId = f.Site.Id });
        var checklist = await new QualityChecklistService(f.Db).CreateAsync(f.Actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = f.Site.Id });
        Assert.Equal(f.Site.Id, observation.ConstructionSiteId);
        Assert.Equal(f.Site.Id, ncr.ConstructionSiteId);
        Assert.Equal(f.Site.Id, checklist.ConstructionSiteId);
        Assert.Single(await new SafetyObservationService(f.Db).GetAllAsync(f.Actor.Id));
        Assert.Single(await new NonConformanceReportService(f.Db).GetAllAsync(f.Actor.Id));
        Assert.Single(await new QualityChecklistService(f.Db).GetAllAsync(f.Actor.Id));
    }
    [Theory]
    [InlineData("observation")]
    [InlineData("ncr")]
    [InlineData("checklist")]
    public async Task Foreign_site_update_leaves_original_record_unchanged(string kind)
    {
        using var f = new Fixture();
        if (kind == "observation")
        {
            var service = new SafetyObservationService(f.Db);
            var row = await service.CreateAsync(f.Actor.Id, new CreateSafetyObservationRequest { ConstructionSiteId = f.Site.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(f.Actor.Id, row.Id, new UpdateSafetyObservationRequest { ConstructionSiteId = f.ForeignSite.Id }));
            Assert.Equal(f.Site.Id, f.Db.SafetyObservations.Single().ConstructionSiteId);
        }
        else if (kind == "ncr")
        {
            var service = new NonConformanceReportService(f.Db);
            var row = await service.CreateAsync(f.Actor.Id, new CreateNonConformanceReportRequest { ConstructionSiteId = f.Site.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(f.Actor.Id, row.Id, new UpdateNonConformanceReportRequest { ConstructionSiteId = f.ForeignSite.Id }));
            Assert.Equal(f.Site.Id, f.Db.NonConformanceReports.Single().ConstructionSiteId);
        }
        else
        {
            var service = new QualityChecklistService(f.Db);
            var row = await service.CreateAsync(f.Actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = f.Site.Id });
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsync(f.Actor.Id, row.Id, new UpdateQualityChecklistRequest { ConstructionSiteId = f.ForeignSite.Id }));
            Assert.Equal(f.Site.Id, f.Db.QualityChecklists.Single().ConstructionSiteId);
        }
    }
    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("empty-company")]
    [InlineData("no-view")]
    public async Task Invalid_actor_or_revoked_view_cannot_read_any_service(string condition)
    {
        using var f = new Fixture();
        if (condition == "inactive") f.Actor.IsActive = false;
        if (condition == "deleted") f.Actor.IsDeleted = true;
        if (condition == "empty-company") f.Actor.CompanyId = Guid.Empty;
        if (condition == "no-view") f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Compliance_View));
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SafetyObservationService(f.Db).GetAllAsync(f.Actor.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new NonConformanceReportService(f.Db).GetAllAsync(f.Actor.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new QualityChecklistService(f.Db).GetAllAsync(f.Actor.Id));
    }
    [Fact]
    public async Task Missing_manage_grant_denies_writes_but_preserves_read_access()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Compliance_Manage)); f.Db.SaveChanges(true);
        Assert.Empty(await new QualityChecklistService(f.Db).GetAllAsync(f.Actor.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SafetyObservationService(f.Db).CreateAsync(f.Actor.Id, new CreateSafetyObservationRequest { ConstructionSiteId = f.Site.Id }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new NonConformanceReportService(f.Db).CreateAsync(f.Actor.Id, new CreateNonConformanceReportRequest { ConstructionSiteId = f.Site.Id }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new QualityChecklistService(f.Db).CreateAsync(f.Actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = f.Site.Id }));
    }
    private sealed class Fixture : IDisposable
    {
        public TestDb Db = new();
        public User Actor = new() { CompanyId = Guid.NewGuid(), IsActive = true, UserRoles = [new() { Role = new Role { Name = "CompanyAdmin", GranularPermissions = [new() { Permission = GranularPermission.Compliance_View }, new() { Permission = GranularPermission.Compliance_Manage }] } }] };
        public ConstructionSite Site = new(), OtherSite = new(), ForeignSite = new() { CompanyId = Guid.NewGuid() };
        public Project ForeignProject = new(), OtherProject = new();
        public Employee ForeignEmployee = new();
        public Fixture()
        {
            Site.CompanyId = OtherSite.CompanyId = Actor.CompanyId!.Value;
            ForeignProject.CompanyId = ForeignEmployee.CompanyId = ForeignSite.CompanyId;
            ForeignProject.ConstructionSiteId = ForeignSite.Id;
            OtherProject.CompanyId = Site.CompanyId; OtherProject.ConstructionSiteId = OtherSite.Id;
            Db.Users.Add(Actor); Db.ConstructionSites.AddRange(Site, OtherSite, ForeignSite); Db.Projects.AddRange(ForeignProject, OtherProject); Db.Employees.Add(ForeignEmployee); Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
