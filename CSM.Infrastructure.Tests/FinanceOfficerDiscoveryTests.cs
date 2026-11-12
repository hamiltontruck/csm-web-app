using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerDiscoveryTests
{
    [Theory]
    [InlineData("sites")] [InlineData("summary")] [InlineData("cost-codes")] [InlineData("site-cost-codes")] [InlineData("executive")] [InlineData("reports")] [InlineData("workspace")] [InlineData("workspace-officer-only")]
    public async Task Discovery_uses_live_sites_and_the_specific_actions_independent_authority(string kind)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor();
        if (kind != "workspace-officer-only") actor.UserRoles.Add(new UserRole { Role = FinanceOfficerAuthorityTests.Role("Accountant", GranularPermission.Finance_Budget_View) }); db.Users.Add(actor);
        var company = actor.CompanyId!.Value; db.Companies.Add(new Company { Id = company, Code = "OWN", Name = "Own", IsActive = true });
        var sites = new[] { new ConstructionSite { CompanyId = company, SiteCode = "A", Name = "Assigned", IsActive = true }, new ConstructionSite { CompanyId = company, SiteCode = "B", Name = "Other", IsActive = true } };
        db.ConstructionSites.AddRange(sites); db.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company, SiteId = sites[0].Id, IsActive = true });
        foreach (var site in sites) {
            db.Budgets.Add(new Budget { CompanyId = company, ConstructionSiteId = site.Id, BudgetNumber = site.SiteCode, TotalAmount = site == sites[0] ? 10m : 100m, Status = BudgetStatus.Approved, EffectiveFrom = new DateOnly(2026,11,11) });
            db.Expenses.Add(new Expense { CompanyId = company, ConstructionSiteId = site.Id, ExpenseNumber = site.SiteCode, Amount = site == sites[0] ? 10m : 100m, CurrencyCode = "ETB" });
            db.CostCodes.Add(new CostCode { CompanyId = company, ConstructionSiteId = site.Id, Code = site.SiteCode, Name = site.Name, IsActive = true });
        }
        await db.SaveChangesAsync();
        if (kind == "sites") {
            var service = new SiteService(db); Assert.Equal(sites[0].Id, Assert.Single(await service.GetAllAsync(actor.Id)).Id);
            Assert.Equal(sites[0].Id, (await service.GetByIdAsync(actor.Id, sites[0].Id)).Id);
            Assert.NotNull(await Record.ExceptionAsync(() => service.GetByIdAsync(actor.Id, sites[1].Id)));
        }
        else if (kind.StartsWith("workspace")) {
            var summary = await new WorkspaceDashboardService(db).GetAsync(actor.Id);
            Assert.Equal(kind == "workspace" ? 110m : 10m, summary.TotalBudget); Assert.Equal(10m, summary.TotalExpenses);
        }
        else if (kind == "reports") {
            var service = new ReportingService(db); var summary = await service.GetAllFinancialSummariesAsync(actor.Id);
            Assert.Null(summary.Company); Assert.Equal(sites[0].Id, Assert.Single(summary.Sites).SiteId); Assert.Equal(10m, summary.Total.TotalBudget);
            Assert.NotNull(await Record.ExceptionAsync(() => service.GetSiteFinancialSummaryAsync(actor.Id, sites[1].Id)));
        }
        else if (kind == "executive") {
            var method = typeof(ExecutiveDashboardService).GetMethod("GetForActorAsync"); Assert.NotNull(method);
            var summary = await (Task<CSM.Application.Dashboard.Dtos.ExecutiveDashboardResponse>)method.Invoke(new ExecutiveDashboardService(db), [actor.Id, company, CancellationToken.None])!;
            Assert.Equal(10m, summary.TotalBudget); Assert.Equal(0, summary.TotalEmployees);
        }
        else if (kind == "summary") {
            var summary = await new FinancialSummaryService(db).GetCompanySummaryAsync(actor.Id);
            Assert.Equal(10m, summary.TotalBudget); Assert.Equal(sites[0].Id, Assert.Single(summary.Sites).ConstructionSiteId);
        }
        else if (kind == "site-cost-codes") {
            var method = typeof(CostCodeService).GetMethods().SingleOrDefault(x => x.Name == "GetBySiteAsync" && x.GetParameters().Length == 3);
            Assert.NotNull(method);
            var service = new CostCodeService(db);
            async Task<IReadOnlyCollection<CSM.Application.Finance.CostCodes.Dtos.CostCodeResponse>> Read(Guid id) =>
                await (Task<List<CSM.Application.Finance.CostCodes.Dtos.CostCodeResponse>>)method.Invoke(service, [actor.Id, id, CancellationToken.None])!;
            Assert.Equal("A", Assert.Single(await Read(sites[0].Id)).Code);
            Assert.NotNull(await Record.ExceptionAsync(() => Read(sites[1].Id)));
        }
        else {
            Assert.Equal("A", Assert.Single(await new CostCodeService(db).GetAllAsync(actor.Id)).Code);
        }
    }
}
