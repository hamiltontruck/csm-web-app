using System.Reflection;
using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ExpenseSettlementRulesTests
{
    [Theory]
    [InlineData("none", 0, 60)] [InlineData("partial", 20, 40)] [InlineData("full", 60, 0)]
    [InlineData("mixed", 60, 0)] [InlineData("reversed", 0, 60)] [InlineData("pending", 0, 60)]
    [InlineData("incoming", 0, 60)] [InlineData("foreign", 0, 60)] [InlineData("other-site", 0, 60)]
    [InlineData("other-currency", 0, 60)] [InlineData("unapproved-cash", 0, 60)] [InlineData("cash-in", 0, 60)]
    [InlineData("inactive-account", 20, 40)]
    public async Task Expense_reads_count_only_its_actual_matching_completed_evidence(string kind, decimal settled, decimal outstanding)
    {
        using var f = new Fixture();
        switch (kind)
        {
            case "partial": f.Payment(20m); break;
            case "full": f.Payment(60m); f.Expense.Status = ExpenseStatus.Paid; break;
            case "mixed": f.Payment(20m); f.Cash(40m); f.Expense.Status = ExpenseStatus.Paid; break;
            case "reversed": f.Payment(60m, PaymentStatus.Reversed); break;
            case "pending": f.Payment(60m, PaymentStatus.Pending); break;
            case "incoming": f.Payment(60m).Direction = PaymentDirection.Incoming; break;
            case "foreign": f.Payment(60m).CompanyId = Guid.NewGuid(); break;
            case "other-site": f.Payment(60m).ConstructionSiteId = Guid.NewGuid(); break;
            case "other-currency": f.Payment(60m).CurrencyCode = "USD"; break;
            case "unapproved-cash": f.Cash(60m).IsApproved = false; break;
            case "cash-in": f.Cash(60m).TransactionType = "CashIn"; break;
            case "inactive-account": f.Cash(20m); f.Account.IsActive = false; break;
        }
        await f.Db.SaveChangesAsync();
        var response = await new ExpenseService(f.Db).GetByIdAsync(f.Actor.Id, f.Expense.Id);
        var summary = JsonSerializer.SerializeToElement(response).GetProperty("Settlement");
        Assert.Equal(60m, summary.GetProperty("TotalDue").GetDecimal());
        Assert.Equal(settled, summary.GetProperty("SettledAmount").GetDecimal());
        Assert.Equal(outstanding, summary.GetProperty("OutstandingAmount").GetDecimal());
        Assert.True(summary.GetProperty("IsVerified").GetBoolean());
        Assert.False(summary.GetProperty("IsLegacyPaid").GetBoolean());
    }

    [Fact]
    public async Task Historical_paid_without_links_is_unverified_and_is_not_changed_by_reading()
    {
        using var f = new Fixture(); f.Expense.Status = ExpenseStatus.Paid; f.Expense.CreatedBy = null; await f.Db.SaveChangesAsync();
        var response = await new ExpenseService(f.Db).GetByIdAsync(f.Actor.Id, f.Expense.Id);
        var summary = JsonSerializer.SerializeToElement(response).GetProperty("Settlement");
        Assert.True(summary.GetProperty("IsLegacyPaid").GetBoolean()); Assert.False(summary.GetProperty("IsVerified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("OutstandingAmount").ValueKind);
        Assert.Equal(ExpenseStatus.Paid, f.Db.Expenses.Single().Status); Assert.Empty(f.Db.Payments); Assert.Empty(f.Db.PettyCashTransactions);
    }

    [Theory]
    [InlineData("payment")] [InlineData("petty-cash")]
    public async Task Options_are_minimal_and_limit_both_expense_read_and_disbursement_scope(string purpose)
    {
        using var f = new Fixture();
        var other = new ConstructionSite { CompanyId = f.Account.CompanyId, SiteCode = "OTHER", Name = "Unassigned", IsActive = true };
        f.Db.ConstructionSites.Add(other);
        f.Db.Expenses.Add(new() { CompanyId = f.Account.CompanyId, ConstructionSiteId = other.Id, CostCodeId = f.Expense.CostCodeId, ExpenseNumber = "SECRET", Status = ExpenseStatus.Approved, Amount = 999m, ApprovedBy = Guid.NewGuid() });
        f.Db.Expenses.Add(new() { CompanyId = f.Account.CompanyId, CostCodeId = f.Expense.CostCodeId, ExpenseNumber = "COMPANY", Status = ExpenseStatus.Approved, Amount = 999m, ApprovedBy = Guid.NewGuid() });
        await f.Db.SaveChangesAsync();
        var list = await Options(f, purpose, f.Account.ConstructionSiteId, purpose == "petty-cash" ? f.Account.Id : null);
        Assert.Equal(1, list.GetArrayLength()); Assert.Equal(f.Expense.Id, list[0].GetProperty("Id").GetGuid());
        Assert.DoesNotContain("SECRET", list.ToString()); Assert.DoesNotContain("COMPANY", list.ToString()); Assert.DoesNotContain("Email", list.ToString());
    }

    [Theory]
    [InlineData("payment", "Expense_View")] [InlineData("payment", "Payment_Manage")]
    [InlineData("petty-cash", "Expense_View")] [InlineData("petty-cash", "PettyCash_Manage")]
    [InlineData("payment", "assignment")] [InlineData("petty-cash", "assignment")]
    public async Task Options_fail_closed_when_either_current_grant_or_assignment_is_removed(string purpose, string remove)
    {
        using var f = new Fixture();
        if (remove == "assignment") f.Db.UserSiteAccesses.Single().IsActive = false;
        else f.Db.RoleGranularPermissions.Remove(f.Actor.UserRoles.Single().Role.GranularPermissions.Single(x => x.Permission.ToString() == "Finance_" + remove));
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ExpenseManagementException>(() => Options(f, purpose, f.Account.ConstructionSiteId, purpose == "petty-cash" ? f.Account.Id : null));
    }

    [Theory]
    [InlineData("read")] [InlineData("write")]
    public async Task Mixed_roles_cannot_combine_one_actions_corporate_scope_with_another_actions_site_scope(string corporate)
    {
        using var f = new Fixture();
        var other = new ConstructionSite { CompanyId = f.Account.CompanyId, SiteCode = "OTHER", Name = "Other", IsActive = true };
        f.Db.ConstructionSites.Add(other);
        f.Db.Expenses.Add(new() { CompanyId = other.CompanyId, ConstructionSiteId = other.Id, CostCodeId = f.Expense.CostCodeId, ExpenseNumber = "SECRET", Status = ExpenseStatus.Approved, Amount = 99m, ApprovedBy = Guid.NewGuid() });
        f.Db.UserRoles.Add(new UserRole { UserId = f.Actor.Id, Role = FinanceOfficerAuthorityTests.Role("Accountant", corporate == "read" ? GranularPermission.Finance_Expense_View : GranularPermission.Finance_Payment_Manage, GranularPermission.Finance_Payment_View) });
        await f.Db.SaveChangesAsync();
        var list = await Options(f, "payment", null, null);
        Assert.Equal(1, list.GetArrayLength()); Assert.Equal(f.Expense.Id, list[0].GetProperty("Id").GetGuid());
    }

    [Theory]
    [InlineData("paid")] [InlineData("draft")] [InlineData("usd")]
    public async Task Cash_account_options_never_offer_unpayable_or_unsupported_currency_expenses(string kind)
    {
        using var f = new Fixture();
        if (kind == "paid") f.Expense.Status = ExpenseStatus.Paid;
        if (kind == "draft") f.Expense.Status = ExpenseStatus.Draft;
        if (kind == "usd") f.Expense.CurrencyCode = "USD";
        await f.Db.SaveChangesAsync();
        var list = await Options(f, "petty-cash", f.Account.ConstructionSiteId, f.Account.Id);
        Assert.Equal(0, list.GetArrayLength());
    }

    [Theory]
    [InlineData("negative")] [InlineData("precision")] [InlineData("overpay")]
    [InlineData("currency")] [InlineData("rate")] [InlineData("petty-usd")]
    [InlineData("missing-approver")] [InlineData("legacy-paid")] [InlineData("unapproved")]
    public async Task New_disbursement_rejects_invalid_amount_currency_or_approval_without_changing_data(string kind)
    {
        using var f = new Fixture();
        decimal amount = 20m, rate = 1m; var currency = "ETB"; var petty = false;
        switch(kind)
        {
            case "negative": amount = -1; break;
            case "precision": amount = 0.00001m; break;
            case "overpay": amount = 61; break;
            case "currency": currency = "USD"; break;
            case "rate": rate = 2; break;
            case "petty-usd": f.Expense.CurrencyCode = currency = "USD"; petty = true; break;
            case "missing-approver": f.Expense.ApprovedBy = null; break;
            case "legacy-paid": f.Expense.Status = ExpenseStatus.Paid; break;
            case "unapproved": f.Expense.Status = ExpenseStatus.Submitted; break;
        }
        await f.Db.SaveChangesAsync(); var original = f.Expense.Status;
        await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseSettlementService(f.Db)
            .ValidateNewSettlementAsync(f.Expense, amount, currency, rate, petty, CancellationToken.None));
        Assert.Equal(original, f.Expense.Status); Assert.Empty(f.Db.Payments); Assert.Empty(f.Db.PettyCashTransactions);
    }

    [Fact]
    public async Task Corporate_options_for_an_authorized_empty_site_are_an_empty_list()
    {
        using var f = new Fixture(); f.Actor.UserRoles.Single().Role.Name = "Accountant";
        var site = new ConstructionSite { CompanyId = f.Account.CompanyId, SiteCode = "EMPTY", Name = "Empty", IsActive = true };
        f.Db.ConstructionSites.Add(site); await f.Db.SaveChangesAsync();
        Assert.Equal(0, (await Options(f, "payment", site.Id, null)).GetArrayLength());
    }

    private static async Task<JsonElement> Options(Fixture f, string purpose, Guid? site, Guid? account)
    {
        var type = typeof(ExpenseService).Assembly.GetType("CSM.Infrastructure.Services.ExpenseSettlementService");
        Assert.NotNull(type);
        var service = Activator.CreateInstance(type, f.Db)!;
        var method = type.GetMethod("GetOptionsAsync"); Assert.NotNull(method);
        var task = (Task)method.Invoke(service, [f.Actor.Id, purpose, site, account, CancellationToken.None])!;
        await task;
        return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly FinanceIndependentApprovalTests.Fixture Base = new();
        internal FinanceOfficerAuthorityTests.TestDb Db => Base.Db;
        internal User Actor => Base.Actor;
        internal readonly Expense Expense;
        internal readonly PettyCashAccount Account;
        internal Fixture()
        {
            Expense = new() { CompanyId = Base.Site.CompanyId, ConstructionSiteId = Base.Site.Id, CostCodeId = Base.Code.Id, ExpenseNumber = "EXP", Description = "Expense", Amount = 50m, TaxAmount = 10m, CurrencyCode = "ETB", ExchangeRate = 1m, Status = ExpenseStatus.Approved, CreatedBy = Base.Actor.Id, UpdatedBy = Base.Actor.Id, ApprovedBy = Guid.NewGuid() };
            Account = new() { CompanyId = Base.Site.CompanyId, ConstructionSiteId = Base.Site.Id, AccountCode = "CASH", AccountName = "Cash", OpeningBalance = 200m, CurrentBalance = 200m, IsActive = true };
            Db.Expenses.Add(Expense); Db.PettyCashAccounts.Add(Account); Db.SaveChanges(true);
        }
        internal Payment Payment(decimal amount, PaymentStatus status = PaymentStatus.Completed)
        {
            var row = new Payment { CompanyId = Expense.CompanyId, ConstructionSiteId = Expense.ConstructionSiteId, PaymentNumber = Guid.NewGuid().ToString(), Amount = amount, CurrencyCode = Expense.CurrencyCode, ExchangeRate = Expense.ExchangeRate, Direction = PaymentDirection.Outgoing, Status = status };
            Link(row, Expense.Id); Db.Payments.Add(row); return row;
        }
        internal PettyCashTransaction Cash(decimal amount)
        {
            var row = new PettyCashTransaction { PettyCashAccount = Account, CostCodeId = Expense.CostCodeId, TransactionType = "CashOut", ReferenceNumber = Guid.NewGuid().ToString(), Description = "Expense", Amount = amount, CreatedByUserId = Actor.Id, IsApproved = true };
            Link(row, Expense.Id); Db.PettyCashTransactions.Add(row); return row;
        }
        private static void Link(object record, Guid expense)
        {
            var property = record.GetType().GetProperty("ExpenseId"); Assert.NotNull(property); property.SetValue(record, expense);
        }
        public void Dispose() => Base.Dispose();
    }
}
