using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ReportsCompanyScopeTests
{
    [Fact]
    public async Task SuperAdmin_can_switch_company_reports_without_a_company_assignment()
    {
        using var db = CreateDatabase(true);
        var service = new ReportingService(db);
        Assert.Equal(1, (await service.GetExecutiveDashboardAsync(ActorId, default, FirstCompany)).TotalProjects);
        Assert.Equal(2, (await service.GetExecutiveDashboardAsync(ActorId, default, SecondCompany)).TotalProjects);
        await service.GetFinancialSummaryAsync(ActorId, default, SecondCompany);
        await service.GetAllFinancialSummariesAsync(ActorId, default, SecondCompany);
    }

    [Fact]
    public async Task Company_user_cannot_select_another_company()
    {
        using var db = CreateDatabase(false);
        var service = new ReportingService(db);
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetExecutiveDashboardAsync(ActorId, default, SecondCompany));
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetFinancialSummaryAsync(ActorId, default, SecondCompany));
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetAllFinancialSummariesAsync(ActorId, default, SecondCompany));
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetSiteExecutiveDashboardAsync(ActorId, Guid.NewGuid(), default, SecondCompany));
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetSiteFinancialSummaryAsync(ActorId, Guid.NewGuid(), default, SecondCompany));
    }

    [Fact]
    public async Task Company_user_defaults_to_their_own_company()
    {
        using var db = CreateDatabase(false);
        Assert.Equal(1, (await new ReportingService(db).GetExecutiveDashboardAsync(ActorId)).TotalProjects);
    }

    [Fact]
    public async Task SuperAdmin_must_select_a_company()
    {
        using var db = CreateDatabase(true);
        var error = await Assert.ThrowsAsync<ReportingManagementException>(() =>
            new ReportingService(db).GetExecutiveDashboardAsync(ActorId));
        Assert.Contains("Select a company", error.Message);
    }

    [Fact]
    public async Task Unknown_or_deleted_companies_are_rejected()
    {
        using var db = CreateDatabase(true);
        var service = new ReportingService(db);
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetExecutiveDashboardAsync(ActorId, default, Guid.NewGuid()));
        db.Companies.Single(x => x.Id == SecondCompany).IsDeleted = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ReportingManagementException>(() =>
            service.GetExecutiveDashboardAsync(ActorId, default, SecondCompany));
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid FirstCompany = Guid.NewGuid();
    private static readonly Guid SecondCompany = Guid.NewGuid();

    private static ApplicationDbContext CreateDatabase(bool superAdmin)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.AddRange(
            new Company { Id = FirstCompany, Code = "FIRST", Name = "First" },
            new Company { Id = SecondCompany, Code = "SECOND", Name = "Second" });
        var role = new Role
        {
            Id = Guid.NewGuid(), Name = superAdmin ? AppRoles.SuperAdmin : AppRoles.CompanyAdmin,
            GranularPermissions =
            [
                new() { Id = Guid.NewGuid(), Permission = GranularPermission.Reports_View },
                new() { Id = Guid.NewGuid(), Permission = GranularPermission.Reports_FinancialSummary_View }
            ]
        };
        db.Users.Add(new User
        {
            Id = ActorId, CompanyId = superAdmin ? null : FirstCompany,
            Email = "test@example.test", NormalizedEmail = "TEST@EXAMPLE.TEST",
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = role }]
        });
        db.Projects.AddRange(
            new Project { Id = Guid.NewGuid(), CompanyId = FirstCompany, Name = "First" },
            new Project { Id = Guid.NewGuid(), CompanyId = SecondCompany, Name = "Second" },
            new Project { Id = Guid.NewGuid(), CompanyId = SecondCompany, Name = "Third" });
        db.SaveChanges();
        return db;
    }
}
