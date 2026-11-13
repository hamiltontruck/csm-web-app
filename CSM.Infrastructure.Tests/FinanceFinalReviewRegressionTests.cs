using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class FinanceFinalReviewRegressionTests
{
    [Theory]
    [InlineData(false, "site")] [InlineData(true, "site")]
    [InlineData(false, "company")] [InlineData(true, "company")]
    [InlineData(false, "payroll")] [InlineData(true, "payroll")]
    [InlineData(false, "project")] [InlineData(true, "project")]
    public async Task Reparenting_requires_write_authority_over_the_original_payment(bool corporateReader, string origin)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        var other = new ConstructionSite { CompanyId = f.Account.CompanyId, SiteCode = "OTHER", Name = "Unassigned", IsActive = true };
        f.Db.ConstructionSites.Add(other);
        if (corporateReader) f.Db.UserRoles.Add(new UserRole { UserId = f.Actor.Id,
            Role = FinanceOfficerAuthorityTests.Role("Accountant", GranularPermission.Finance_Payment_View, GranularPermission.Finance_Expense_View) });
        var original = new Payment { CompanyId = f.Expense.CompanyId, ConstructionSiteId = origin == "site" ? other.Id : origin == "company" ? null : f.Expense.ConstructionSiteId,
            PayrollRecordId = origin == "payroll" ? Guid.NewGuid() : null, ProjectId = origin == "project" ? Guid.NewGuid() : null,
            PaymentNumber = "ORIGINAL", Amount = 7m, Direction = PaymentDirection.Outgoing, Status = PaymentStatus.Pending, Notes = "Original" };
        f.Db.Payments.Add(original); await f.Db.SaveChangesAsync();
        var site = original.ConstructionSiteId; var payroll = original.PayrollRecordId; var project = original.ProjectId;
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).UpdateAsync(f.Actor.Id, original.Id, Update(f)));
        Assert.Null(original.ExpenseId); Assert.Equal(site, original.ConstructionSiteId); Assert.Equal(payroll, original.PayrollRecordId);
        Assert.Equal(project, original.ProjectId); Assert.Equal(7m, original.Amount); Assert.Equal("Original", original.Notes);
        Assert.Equal(ExpenseStatus.Approved, f.Expense.Status);
    }

    [Fact]
    public async Task Authorized_own_site_pending_payment_can_change_to_expense_source()
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        var payment = new Payment { CompanyId = f.Expense.CompanyId, ConstructionSiteId = f.Expense.ConstructionSiteId, PaymentNumber = "OWN", Amount = 7m, Status = PaymentStatus.Pending };
        f.Db.Payments.Add(payment); await f.Db.SaveChangesAsync();
        var result = await new PaymentService(f.Db).UpdateAsync(f.Actor.Id, payment.Id, Update(f));
        Assert.Equal(f.Expense.Id, result.ExpenseId); Assert.Equal(20m, result.Amount);
    }

    [Theory]
    [InlineData("budget", false)] [InlineData("expense", false)] [InlineData("line", false)]
    [InlineData("budget", true)] [InlineData("expense", true)] [InlineData("line", true)]
    public async Task Only_a_real_normalized_material_change_replaces_the_last_editor(string kind, bool changed)
    {
        using var f = new FinanceIndependentApprovalTests.Fixture();
        var other = FinanceOfficerAuthorityTests.Actor(); other.CompanyId = f.Actor.CompanyId; other.Email = "third@example.test"; other.NormalizedEmail = "THIRD@EXAMPLE.TEST";
        f.Db.Users.Add(other); f.Db.UserSiteAccesses.Add(new() { UserId = other.Id, CompanyId = f.Site.CompanyId, SiteId = f.Site.Id, IsActive = true });
        var creator = Guid.NewGuid(); var date = new DateOnly(2026,11,13);
        if (kind == "expense")
        {
            var row = new Expense { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, CostCodeId = f.Code.Id, ExpenseNumber = "EDIT", ExpenseDate = date,
                Description = "Expense", Amount = 50m, TaxAmount = 10m, CurrencyCode = "ETB", ExchangeRate = 1m, ReferenceNumber = "REF", ReceiptDocumentUrl = "receipt", Status = ExpenseStatus.Submitted,
                CreatedBy = creator, UpdatedBy = f.Actor.Id };
            f.Db.Expenses.Add(row); await f.Db.SaveChangesAsync(); var service = new ExpenseService(f.Db);
            await service.UpdateAsync(other.Id, row.Id, new(f.Site.Id, null, f.Code.Id, null, date, changed ? "Updated" : " Expense ", 50m, 10m, "etb", 1m, " REF ", " receipt "));
            Assert.Equal(changed ? other.Id : f.Actor.Id, row.UpdatedBy);
            var denied = changed ? other.Id : f.Actor.Id; var allowed = changed ? f.Actor.Id : other.Id;
            await Assert.ThrowsAsync<ExpenseManagementException>(() => service.ChangeStatusAsync(denied, row.Id, new(ExpenseStatus.Approved)));
            await service.ChangeStatusAsync(allowed, row.Id, new(ExpenseStatus.Approved)); Assert.Equal(allowed, row.ApprovedBy);
        }
        else
        {
            var row = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, BudgetNumber = "EDIT", Name = "Budget", Description = "Notes", TotalAmount = 100m,
                CurrencyCode = "ETB", EffectiveFrom = date, Status = BudgetStatus.PendingApproval, CreatedBy = creator, UpdatedBy = f.Actor.Id };
            f.Db.Budgets.Add(row);
            BudgetLine? line = null;
            if (kind == "line") { line = new() { CompanyId = f.Site.CompanyId, BudgetId = row.Id, CostCodeId = f.Code.Id, Description = "Line", BudgetedAmount = 100m, RevisedAmount = 100m }; f.Db.BudgetLines.Add(line); }
            await f.Db.SaveChangesAsync(); var service = new BudgetService(f.Db, new PermissionEvaluator(f.Db));
            if (line is not null) await new BudgetLineService(f.Db).UpdateAsync(other.Id, line.Id, new(f.Code.Id, changed ? "Updated" : " Line ", 100m, 100m));
            else await service.UpdateAsync(other.Id, row.Id, new(changed ? "Updated" : " Budget ", " Notes ", 100m, "etb", date, null));
            Assert.Equal(changed ? other.Id : f.Actor.Id, row.UpdatedBy);
            var denied = changed ? other.Id : f.Actor.Id; var allowed = changed ? f.Actor.Id : other.Id;
            await Assert.ThrowsAsync<BudgetManagementException>(() => service.ChangeStatusAsync(denied, row.Id, new(BudgetStatus.Approved)));
            await service.ChangeStatusAsync(allowed, row.Id, new(BudgetStatus.Approved)); Assert.Equal(allowed, row.ApprovedBy);
        }
    }

    [Theory]
    [InlineData("payment", "self")] [InlineData("petty-cash", "self")]
    [InlineData("payment", "unknown")] [InlineData("petty-cash", "unknown")]
    [InlineData("payment", "other")] [InlineData("petty-cash", "other")]
    public async Task Historical_approved_obligations_remain_payable_without_rewriting_their_approval(string purpose, string author)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        f.Expense.CreatedBy = author == "unknown" ? null : author == "self" ? f.Actor.Id : Guid.NewGuid();
        f.Expense.UpdatedBy = f.Expense.CreatedBy; f.Expense.ApprovedBy = f.Actor.Id;
        f.Expense.ApprovedAtUtc = new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc); await f.Db.SaveChangesAsync();
        var approval = f.Expense.ApprovedAtUtc; var creator = f.Expense.CreatedBy;
        var offered = await new ExpenseSettlementService(f.Db).GetForExpenseAsync(f.Actor.Id, f.Expense.Id, purpose);
        Assert.Equal(f.Expense.Id, offered.Expense.Id);
        if (purpose == "payment")
        {
            var payments = new PaymentService(f.Db); var pending = await payments.CreateAsync(f.Actor.Id, ExpensePaymentSettlementTests.Request(f, 60m));
            await payments.CompletePaymentAsync(f.Actor.Id, pending.Id);
        }
        else await new PettyCashTransactionService(f.Db).CreateAsync(f.Actor.Id, ExpensePettySettlementTests.Request(f, 60m));
        Assert.Equal(ExpenseStatus.Paid, f.Expense.Status); Assert.Equal(f.Actor.Id, f.Expense.ApprovedBy);
        Assert.Equal(approval, f.Expense.ApprovedAtUtc); Assert.Equal(creator, f.Expense.CreatedBy);
    }

    private static UpdatePaymentRequest Update(ExpenseSettlementRulesTests.Fixture f) => new(f.Expense.ConstructionSiteId, f.Expense.ProjectId, null, f.Expense.VendorId, null,
        DateOnly.FromDateTime(DateTime.UtcNow), PaymentDirection.Outgoing, 20m, "ETB", 1m, "Cash", "REF", "Edited") { ExpenseId = f.Expense.Id };
}
