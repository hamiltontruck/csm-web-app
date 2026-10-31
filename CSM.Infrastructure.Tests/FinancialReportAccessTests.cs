using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;
public sealed class FinancialReportAccessTests
{
    [Fact]
    public async Task Financial_summary_permission_is_required_at_the_service_boundary()
    {
        using var f = new Fixture("CompanyAdmin", GranularPermission.Reports_View);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new FinancialSummaryService(f.Db).GetCompanySummaryAsync(f.Actor.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new FinancialSummaryService(f.Db).GetSiteSummaryAsync(f.Actor.Id, f.Site));
    }

    [Fact]
    public async Task Site_scoped_summary_excludes_company_level_unassigned_and_foreign_finance()
    {
        using var f = new Fixture("SiteManager", GranularPermission.Reports_FinancialSummary_View);
        var summary = await new FinancialSummaryService(f.Db).GetCompanySummaryAsync(f.Actor.Id);
        Assert.Equal(10m, summary.TotalBudget);
        Assert.Equal(1m, summary.TotalExpenses);
        Assert.Equal(f.Site, Assert.Single(summary.Sites).ConstructionSiteId);
    }

    [Fact]
    public async Task Company_finance_reader_retains_company_and_site_records_without_cross_company_data()
    {
        using var f = new Fixture("Accountant", GranularPermission.Reports_FinancialSummary_View);
        var summary = await new FinancialSummaryService(f.Db).GetCompanySummaryAsync(f.Actor.Id);
        Assert.Equal(130m, summary.TotalBudget);
        Assert.Equal(3m, summary.TotalExpenses);
        Assert.Equal(2, summary.Sites.Count);
    }

    [Fact]
    public async Task Site_detail_denies_unassigned_and_foreign_sites_before_returning_finance()
    {
        using var f = new Fixture("SiteManager", GranularPermission.Reports_FinancialSummary_View);
        var service = new FinancialSummaryService(f.Db);
        Assert.Equal(10m, (await service.GetSiteSummaryAsync(f.Actor.Id, f.Site)).BudgetAmount);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSiteSummaryAsync(f.Actor.Id, f.OtherSite));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSiteSummaryAsync(f.Actor.Id, f.ForeignSite));
    }

    [Fact]
    public async Task Operational_reports_omit_all_financial_fields_without_financial_grant()
    {
        using var f = new Fixture("CompanyAdmin", GranularPermission.Reports_View);
        var service = new ReportingService(f.Db);
        var report = await service.GetExecutiveDashboardAsync(f.Actor.Id);
        Assert.Equal(2, report.TotalProjects);
        Assert.Null(report.TotalBudget);
        Assert.Null(report.TotalContractValue);
        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("TotalBudget", json);
        Assert.DoesNotContain("TotalExpenses", json);
        Assert.DoesNotContain("TotalContractValue", json);
        var site = await service.GetSiteExecutiveDashboardAsync(f.Actor.Id, f.Site);
        Assert.Equal(1, site.TotalProjects);
        Assert.Null(site.TotalBudget);
    }

    [Fact]
    public async Task Operational_reports_respect_assigned_sites_even_with_report_permission()
    {
        using var f = new Fixture("Supervisor", GranularPermission.Reports_View);
        var service = new ReportingService(f.Db);
        var report = await service.GetExecutiveDashboardAsync(f.Actor.Id);
        Assert.Equal(1, report.TotalProjects);
        Assert.Equal(1, report.TotalSites);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSiteExecutiveDashboardAsync(f.Actor.Id, f.OtherSite));
    }

    [Fact]
    public async Task Site_report_reader_cannot_call_company_only_financial_report()
    {
        using var f = new Fixture("SiteManager", GranularPermission.Reports_FinancialSummary_View);
        var service = new ReportingService(f.Db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetFinancialSummaryAsync(f.Actor.Id));
        var report = await service.GetAllFinancialSummariesAsync(f.Actor.Id);
        Assert.Null(report.Company);
        Assert.Equal(10m, report.Total.TotalBudget);
        Assert.Equal(1m, report.Total.TotalExpenses);
        Assert.Equal(f.Site, Assert.Single(report.Sites).SiteId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSiteFinancialSummaryAsync(f.Actor.Id, f.OtherSite));
    }

    [Fact]
    public async Task Revoked_site_access_removes_financial_totals_instead_of_falling_back_to_company()
    {
        using var f = new Fixture("SiteManager", GranularPermission.Reports_FinancialSummary_View);
        f.Db.UserSiteAccesses.Single().IsActive = false; f.Db.SaveChanges(true);
        var result = await new FinancialSummaryService(f.Db).GetCompanySummaryAsync(f.Actor.Id);
        Assert.Equal(0m, result.TotalBudget);
        Assert.Empty(result.Sites);
    }

    [Fact]
    public async Task Disabled_actor_cannot_use_direct_summary_services()
    {
        using var f = new Fixture("CompanyAdmin", GranularPermission.Reports_View, GranularPermission.Reports_FinancialSummary_View);
        f.Actor.IsActive = false; f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new FinancialSummaryService(f.Db).GetCompanySummaryAsync(f.Actor.Id));
        await Assert.ThrowsAsync<ReportingManagementException>(() => new ReportingService(f.Db).GetExecutiveDashboardAsync(f.Actor.Id));
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Guid Company = Guid.NewGuid(), OtherCompany = Guid.NewGuid(), Site = Guid.NewGuid(), OtherSite = Guid.NewGuid(), ForeignSite = Guid.NewGuid();
        public User Actor;
        public Fixture(string role, params GranularPermission[] permissions)
        {
            Actor = new User { CompanyId = Company, IsActive = true, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = permissions.Select(p => new RoleGranularPermission { Permission = p }).ToList() } }] };
            Db.Users.Add(Actor);
            Db.Companies.AddRange(new Company { Id = Company, Name = "Own" }, new Company { Id = OtherCompany, Name = "Foreign" });
            Db.ConstructionSites.AddRange(new ConstructionSite { Id = Site, CompanyId = Company, Name = "Assigned" }, new ConstructionSite { Id = OtherSite, CompanyId = Company, Name = "Unassigned" }, new ConstructionSite { Id = ForeignSite, CompanyId = OtherCompany, Name = "Foreign" });
            Db.UserSiteAccesses.Add(new() { UserId = Actor.Id, CompanyId = Company, SiteId = Site, IsActive = true });
            Db.Projects.AddRange(new Project { CompanyId = Company, ConstructionSiteId = Site, ContractValue = 11 }, new Project { CompanyId = Company, ConstructionSiteId = OtherSite, ContractValue = 22 }, new Project { CompanyId = OtherCompany, ConstructionSiteId = ForeignSite, ContractValue = 999 });
            Db.Budgets.AddRange(new Budget { CompanyId = Company, TotalAmount = 100, Status = BudgetStatus.Active }, new Budget { CompanyId = Company, ConstructionSiteId = Site, TotalAmount = 10, Status = BudgetStatus.Active }, new Budget { CompanyId = Company, ConstructionSiteId = OtherSite, TotalAmount = 20, Status = BudgetStatus.Active }, new Budget { CompanyId = OtherCompany, ConstructionSiteId = ForeignSite, TotalAmount = 999, Status = BudgetStatus.Active });
            Db.Expenses.AddRange(new Expense { CompanyId = Company, ConstructionSiteId = Site, Amount = 1, Status = ExpenseStatus.Approved }, new Expense { CompanyId = Company, ConstructionSiteId = OtherSite, Amount = 2, Status = ExpenseStatus.Approved }, new Expense { CompanyId = OtherCompany, ConstructionSiteId = ForeignSite, Amount = 999, Status = ExpenseStatus.Approved });
            Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
}
