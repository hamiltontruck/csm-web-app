using CSM.Application.Features.Compliance.NonConformanceReports;
using CSM.Application.Features.Compliance.QualityChecklists;
using CSM.Application.Features.Compliance.SafetyObservations.DTOs;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.HRM;
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
    private sealed class Fixture : IDisposable
    {
        public TestDb Db = new();
        public User Actor = new() { CompanyId = Guid.NewGuid(), IsActive = true };
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
