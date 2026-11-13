using Microsoft.EntityFrameworkCore;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class ExpensePaymentSettlementTests
{
    [Theory]
    [InlineData(false, 60)] [InlineData(true, 60)] [InlineData(false, 20)] [InlineData(true, 20)]
    public async Task Both_completion_paths_settle_only_actual_amount_and_reversal_restores_outstanding(bool dedicated, decimal amount)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        var approver = f.Expense.ApprovedBy;
        var service = new PaymentService(f.Db);
        var pending = await service.CreateAsync(f.Actor.Id, Request(f, amount));
        Assert.Equal(ExpenseStatus.Approved, f.Expense.Status); Assert.Equal(PaymentStatus.Pending, pending.Status);
        if (dedicated) await service.CompletePaymentAsync(f.Actor.Id, pending.Id);
        else await service.ChangeStatusAsync(f.Actor.Id, pending.Id, new(PaymentStatus.Completed));
        Assert.Equal(amount == 60m ? ExpenseStatus.Paid : ExpenseStatus.Approved, f.Expense.Status);
        var summary = await new ExpenseSettlementService(f.Db).GetSummaryAsync(f.Expense, default);
        Assert.Equal(amount, summary.SettledAmount); Assert.Equal(60m - amount, summary.OutstandingAmount);
        if (dedicated) await service.ReversePaymentAsync(f.Actor.Id, pending.Id);
        else await service.ChangeStatusAsync(f.Actor.Id, pending.Id, new(PaymentStatus.Reversed));
        Assert.Equal(ExpenseStatus.Approved, f.Expense.Status); Assert.Equal(approver, f.Expense.ApprovedBy);
        Assert.Equal(60m, (await new ExpenseSettlementService(f.Db).GetSummaryAsync(f.Expense, default)).OutstandingAmount);
    }

    [Fact]
    public async Task Status_only_paid_without_evidence_cannot_create_a_disbursement()
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseService(f.Db).ChangeStatusAsync(f.Actor.Id, f.Expense.Id, new(ExpenseStatus.Paid)));
        Assert.Equal(ExpenseStatus.Approved, f.Expense.Status); Assert.Empty(f.Db.Payments); Assert.Empty(f.Db.PettyCashTransactions);
    }

    [Theory]
    [InlineData("invoice")] [InlineData("payroll")] [InlineData("incoming")]
    [InlineData("site")] [InlineData("currency")] [InlineData("rate")]
    [InlineData("precision")] [InlineData("overpay")] [InlineData("legacy-paid")]
    public async Task Invalid_new_payment_is_rejected_without_evidence(string kind)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        var request = Request(f, kind == "overpay" ? 61m : kind == "precision" ? 0.00001m : 20m);
        request = kind switch
        {
            "invoice" => request with { InvoiceId = Guid.NewGuid() },
            "payroll" => request with { PayrollRecordId = Guid.NewGuid() },
            "incoming" => request with { Direction = PaymentDirection.Incoming },
            "site" => request with { ConstructionSiteId = Guid.NewGuid() },
            "currency" => request with { CurrencyCode = "USD" },
            "rate" => request with { ExchangeRate = 2m }, _ => request
        };
        if (kind == "legacy-paid") f.Expense.Status = ExpenseStatus.Paid;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).CreateAsync(f.Actor.Id, request));
        Assert.Empty(f.Db.Payments);
    }

    [Fact]
    public async Task Pending_payments_reserve_nothing_and_completion_rechecks_remaining()
    {
        using var f = new ExpenseSettlementRulesTests.Fixture(); var service = new PaymentService(f.Db);
        var first = await service.CreateAsync(f.Actor.Id, Request(f, 40m));
        var second = await service.CreateAsync(f.Actor.Id, Request(f, 40m));
        await service.CompletePaymentAsync(f.Actor.Id, first.Id);
        await Assert.ThrowsAsync<PaymentManagementException>(() => service.ChangeStatusAsync(f.Actor.Id, second.Id, new(PaymentStatus.Completed)));
        Assert.Equal(PaymentStatus.Pending, f.Db.Payments.Single(x => x.Id == second.Id).Status);
        Assert.Equal(40m, (await new ExpenseSettlementService(f.Db).GetSummaryAsync(f.Expense, default)).SettledAmount);
    }

    [Theory]
    [InlineData("Expense_View")] [InlineData("Payment_Manage")] [InlineData("assignment")]
    public async Task Revoked_scope_denies_completion_without_changes(string removed)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture(); var service = new PaymentService(f.Db);
        var pending = await service.CreateAsync(f.Actor.Id, Request(f, 60m));
        if (removed == "assignment") f.Db.UserSiteAccesses.Single().IsActive = false;
        else f.Db.RoleGranularPermissions.Remove(f.Actor.UserRoles.Single().Role.GranularPermissions.Single(x => x.Permission.ToString() == "Finance_" + removed));
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<PaymentManagementException>(() => service.CompletePaymentAsync(f.Actor.Id, pending.Id));
        Assert.Equal(PaymentStatus.Pending, f.Db.Payments.Single().Status); Assert.Equal(ExpenseStatus.Approved, f.Expense.Status);
    }

    [Fact]
    public async Task Pending_amount_edits_and_delete_work_but_completed_evidence_is_immutable()
    {
        using var f = new ExpenseSettlementRulesTests.Fixture(); var service = new PaymentService(f.Db);
        var pending = await service.CreateAsync(f.Actor.Id, Request(f, 20m));
        var update = new UpdatePaymentRequest(f.Expense.ConstructionSiteId, null, null, null, null,
            DateOnly.FromDateTime(DateTime.UtcNow), PaymentDirection.Outgoing, 30m, "ETB", 1m, "Cash", "REF", "Edited") { ExpenseId = f.Expense.Id };
        var changed = await service.UpdateAsync(f.Actor.Id, pending.Id, update); Assert.Equal(30m, changed.Amount);
        await service.CompletePaymentAsync(f.Actor.Id, pending.Id);
        await Assert.ThrowsAsync<PaymentManagementException>(() => service.UpdateAsync(f.Actor.Id, pending.Id, update));
        await Assert.ThrowsAsync<PaymentManagementException>(() => service.DeleteAsync(f.Actor.Id, pending.Id));
        var another = await service.CreateAsync(f.Actor.Id, Request(f, 10m));
        await service.DeleteAsync(f.Actor.Id, another.Id);
        Assert.True(f.Db.Payments.IgnoreQueryFilters().Single(x => x.Id == another.Id).IsDeleted);
        Assert.Equal(30m, (await new ExpenseSettlementService(f.Db).GetSummaryAsync(f.Expense, default)).SettledAmount);
    }

    internal static CreatePaymentRequest Request(ExpenseSettlementRulesTests.Fixture f, decimal amount)
    {
        var request = new CreatePaymentRequest(f.Expense.ConstructionSiteId, f.Expense.ProjectId, null, f.Expense.VendorId, null,
            Guid.NewGuid().ToString(), DateOnly.FromDateTime(DateTime.UtcNow), PaymentDirection.Outgoing, amount, "ETB", 1m, "Cash", "REF", "Expense");
        var link = typeof(CreatePaymentRequest).GetProperty("ExpenseId"); Assert.NotNull(link); link.SetValue(request, f.Expense.Id); return request;
    }
}
