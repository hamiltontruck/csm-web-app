using CSM.Application.Features.Compliance.NonConformanceReports;
using CSM.Application.Features.Compliance.QualityChecklists;
using CSM.Application.Features.Compliance.SafetyObservations.DTOs;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Compliance;
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
    [Fact]
    public async Task PostgreSQL_translates_parent_scopes_and_excludes_legacy_foreign_links()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "compliance_scope_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "COMP", Name = "Own" };
            var foreignCompany = new Company { Code = "OTHER", Name = "Other" };
            var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "ONE", Name = "Own site" };
            var foreign = new ConstructionSite { CompanyId = foreignCompany.Id, SiteCode = "OTHER", Name = "Foreign site" };
            var actor = new User { CompanyId = company.Id, Email = "compliance@example.test", NormalizedEmail = "COMPLIANCE@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = new Role { Name = "CompanyAdmin", NormalizedName = "COMPANYADMIN", GranularPermissions = [new() { Permission = GranularPermission.Compliance_View }, new() { Permission = GranularPermission.Compliance_Manage }] } }] };
            db.Companies.AddRange(company, foreignCompany); db.ConstructionSites.AddRange(site, foreign); db.Users.Add(actor); await db.SaveChangesAsync();
            var observations = new SafetyObservationService(db); var ncrs = new NonConformanceReportService(db); var checklists = new QualityChecklistService(db);
            await observations.CreateAsync(actor.Id, new CreateSafetyObservationRequest { ConstructionSiteId = site.Id, ObservationNumber = "ONE", ObservationDate = DateTime.UtcNow });
            await ncrs.CreateAsync(actor.Id, new CreateNonConformanceReportRequest { ConstructionSiteId = site.Id, NcrNumber = "ONE", Title = "Own", ReportedDate = DateTime.UtcNow });
            await checklists.CreateAsync(actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = site.Id, Name = "Own", Category = "Test" });
            db.SafetyObservations.Add(new SafetyObservation { CompanyId = company.Id, ConstructionSiteId = foreign.Id, ObserverId = actor.Id, ObservationNumber = "LEGACY", ObservationDate = DateTime.UtcNow });
            db.NonConformanceReports.Add(new NonConformanceReport { CompanyId = company.Id, ConstructionSiteId = foreign.Id, RaisedById = actor.Id, NcrNumber = "LEGACY", ReportedDate = DateTime.UtcNow });
            db.QualityChecklists.Add(new QualityChecklist { CompanyId = company.Id, ConstructionSiteId = foreign.Id, Name = "Legacy" });
            await db.SaveChangesAsync();
            Assert.Single(await observations.GetAllAsync(actor.Id));
            Assert.Single(await ncrs.GetAllAsync(actor.Id));
            Assert.Single(await checklists.GetAllAsync(actor.Id));
            site.IsDeleted = true; await db.SaveChangesAsync();
            Assert.Single(await observations.GetAllAsync(actor.Id));
            Assert.Single(await ncrs.GetAllAsync(actor.Id));
            Assert.Single(await checklists.GetAllAsync(actor.Id));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => observations.CreateAsync(actor.Id, new CreateSafetyObservationRequest { ConstructionSiteId = foreign.Id }));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => ncrs.CreateAsync(actor.Id, new CreateNonConformanceReportRequest { ConstructionSiteId = foreign.Id }));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => checklists.CreateAsync(actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = foreign.Id }));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    [Fact]
    public async Task Same_company_history_remains_readable_when_parents_are_soft_deleted()
    {
        using var f = new Fixture();
        var project = new Project { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id };
        var employee = new Employee { CompanyId = f.Site.CompanyId };
        f.Db.Projects.Add(project); f.Db.Employees.Add(employee); f.Db.SaveChanges(true);
        var observations = new SafetyObservationService(f.Db); var ncrs = new NonConformanceReportService(f.Db); var checklists = new QualityChecklistService(f.Db);
        var observation = await observations.CreateAsync(f.Actor.Id, new CreateSafetyObservationRequest { ConstructionSiteId = f.Site.Id, ProjectId = project.Id, EmployeeId = employee.Id });
        var ncr = await ncrs.CreateAsync(f.Actor.Id, new CreateNonConformanceReportRequest { ConstructionSiteId = f.Site.Id, ProjectId = project.Id, AssignedToEmployeeId = employee.Id });
        var checklist = await checklists.CreateAsync(f.Actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = f.Site.Id });
        project.IsDeleted = true; employee.IsDeleted = true; f.Site.IsDeleted = true; f.Db.SaveChanges(true);
        Assert.Single(await observations.GetAllAsync(f.Actor.Id));
        Assert.Single(await ncrs.GetAllAsync(f.Actor.Id));
        Assert.Single(await checklists.GetAllAsync(f.Actor.Id));
        Assert.Equal(observation.Id, (await observations.GetByIdAsync(f.Actor.Id, observation.Id)).Id);
        Assert.Equal(ncr.Id, (await ncrs.GetByIdAsync(f.Actor.Id, ncr.Id))!.Id);
        Assert.Equal(checklist.Id, (await checklists.GetByIdAsync(f.Actor.Id, checklist.Id))!.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => checklists.CreateAsync(f.Actor.Id, new CreateQualityChecklistRequest { ConstructionSiteId = f.Site.Id }));
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
