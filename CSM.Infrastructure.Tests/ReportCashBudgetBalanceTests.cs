using System.Text.Json;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ReportCashBudgetBalanceTests
{
    [Theory]
    [InlineData("site", 40, -50)]
    [InlineData("company", 40, -50)]
    [InlineData("assigned", 40, -50)]
    [InlineData("all", 80, -100)]
    public async Task Budget_is_not_cash_and_overspending_remains_visible(string kind, decimal cash, decimal remaining)
    {
        using var db = FinanceOfficerAuthorityTests.Database();
        var actor = FinanceOfficerAuthorityTests.Actor();
        if (kind is "company" or "all")
            actor.UserRoles = [new UserRole { Role = FinanceOfficerAuthorityTests.Role("Accountant", GranularPermission.Reports_FinancialSummary_View) }];
        db.Users.Add(actor);
        var company = actor.CompanyId!.Value;
        db.Companies.Add(new Company { Id = company, Code = "TEST", Name = "Test", IsActive = true });
        var site = new ConstructionSite { CompanyId = company, SiteCode = "SITE", Name = "Assigned", IsActive = true };
        db.ConstructionSites.Add(site);
        db.UserSiteAccesses.Add(new() { CompanyId = company, UserId = actor.Id, SiteId = site.Id, IsActive = true });
        foreach (var siteId in new Guid?[] { null, site.Id })
        {
            db.Budgets.Add(new Budget { CompanyId = company, ConstructionSiteId = siteId, BudgetNumber = Guid.NewGuid().ToString(), TotalAmount = 1000m, Status = BudgetStatus.Active });
            db.Expenses.Add(new Expense { CompanyId = company, ConstructionSiteId = siteId, Amount = 1000m, Status = ExpenseStatus.Approved });
            db.Expenses.Add(new Expense { CompanyId = company, ConstructionSiteId = siteId, Amount = 40m, TaxAmount = 10m, Status = ExpenseStatus.Paid });
            db.Expenses.Add(new Expense { CompanyId = company, ConstructionSiteId = siteId, Amount = 999m, Status = ExpenseStatus.Cancelled });
            db.Payments.Add(new Payment { CompanyId = company, ConstructionSiteId = siteId, Amount = 100m, Direction = PaymentDirection.Outgoing, Status = PaymentStatus.Reversed });
            var account = new PettyCashAccount { CompanyId = company, ConstructionSiteId = siteId, AccountCode = Guid.NewGuid().ToString(), AccountName = "Cash", IsActive = true, OpeningBalance = 25m, CurrentBalance = 65m };
            db.PettyCashAccounts.Add(account);
            db.PettyCashTransactions.AddRange(
                new() { PettyCashAccount = account, TransactionType = "CashIn", Amount = 200m, IsApproved = true },
                new() { PettyCashAccount = account, TransactionType = "CashOut", Amount = 150m, IsApproved = true },
                new() { PettyCashAccount = account, TransactionType = "CashOut", Amount = 10m, IsApproved = true });
        }
        await db.SaveChangesAsync();
        var service = new ReportingService(db);
        var report = kind switch
        {
            "site" => await service.GetSiteFinancialSummaryAsync(actor.Id, site.Id),
            "company" => await service.GetFinancialSummaryAsync(actor.Id),
            _ => (await service.GetAllFinancialSummariesAsync(actor.Id)).Total
        };
        Assert.Equal(cash, report.Balance);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.Equal(cash, json.RootElement.GetProperty("CashBalance").GetDecimal());
        Assert.Equal(remaining, json.RootElement.GetProperty("BudgetRemaining").GetDecimal());
        Assert.Equal(cash, report.NetCashFlow);
        Assert.Equal(kind == "all" ? 130m : 65m, report.PettyCashBalance);
    }
}
