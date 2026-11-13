using CSM.Application.Finance.PettyCashTransactions.Dtos;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed partial class ExpenseSettlementPostgresTests
{
    [Fact]
    public async Task Concurrent_payment_and_cash_out_cannot_overpay_the_same_expense()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture); Guid paymentId;
        await using (var db = fixture.Context()) paymentId = (await new PaymentService(db).CreateAsync(data.ActorId, Request(data, 40m))).Id;
        await using var one = fixture.Context(); await using var two = fixture.Context();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => new PaymentService(one).CompletePaymentAsync(data.ActorId, paymentId)),
            Record.ExceptionAsync(() => new PettyCashTransactionService(two).CreateAsync(data.ActorId, CashRequest(data, 40m))));
        Assert.Single(errors, x => x is null);
        await using var verify = fixture.Context(); var expense = await verify.Expenses.SingleAsync();
        Assert.Equal(40m, (await new ExpenseSettlementService(verify).GetSummaryAsync(expense, default)).SettledAmount);
        var cash = await verify.PettyCashTransactions.SumAsync(x => x.Amount);
        Assert.Equal(100m - cash, (await verify.PettyCashAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Linked_and_unlinked_withdrawals_share_one_account_lock()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture);
        var linked = CashRequest(data, 20m); var direct = CashRequest(data, 90m); direct.ExpenseId = null;
        await using var one = fixture.Context(); await using var two = fixture.Context();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => new PettyCashTransactionService(one).CreateAsync(data.ActorId, linked)),
            Record.ExceptionAsync(() => new PettyCashTransactionService(two).CreateAsync(data.ActorId, direct)));
        Assert.Single(errors, x => x is null);
        await using var verify = fixture.Context(); Assert.Single(await verify.PettyCashTransactions.ToListAsync());
        Assert.Equal(100m - await verify.PettyCashTransactions.SumAsync(x => x.Amount), (await verify.PettyCashAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Concurrent_duplicate_partial_reference_debits_only_once()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture);
        var request = CashRequest(data, 20m);
        await using var one = fixture.Context(); await using var two = fixture.Context();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => new PettyCashTransactionService(one).CreateAsync(data.ActorId, request)),
            Record.ExceptionAsync(() => new PettyCashTransactionService(two).CreateAsync(data.ActorId, request)));
        Assert.Single(errors, x => x is null);
        await using var verify = fixture.Context(); Assert.Single(await verify.PettyCashTransactions.ToListAsync());
        Assert.Equal(80m, (await verify.PettyCashAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task Cash_settlement_failure_rolls_back_the_account_and_posted_cash_row()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture);
        await using (var fail = new FailingSettlementDb(fixture.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PettyCashTransactionService(fail).CreateAsync(data.ActorId, CashRequest(data, 60m)));
        await using var verify = fixture.Context(); Assert.Empty(await verify.PettyCashTransactions.ToListAsync());
        Assert.Equal(100m, (await verify.PettyCashAccounts.SingleAsync()).CurrentBalance);
        Assert.Equal(ExpenseStatus.Approved, (await verify.Expenses.SingleAsync()).Status);
    }

    [Fact]
    public async Task Excess_precision_is_rejected_before_postgres_can_round_the_amount()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture);
        await using (var db = fixture.Context())
            Assert.NotNull(await Record.ExceptionAsync(() => new PettyCashTransactionService(db).CreateAsync(data.ActorId, CashRequest(data, 0.00001m))));
        await using var verify = fixture.Context(); Assert.Empty(await verify.PettyCashTransactions.ToListAsync());
        Assert.Equal(100m, (await verify.PettyCashAccounts.SingleAsync()).CurrentBalance);
    }

    private static CreatePettyCashTransactionRequest CashRequest(Seed data, decimal amount) => new()
    {
        PettyCashAccountId = data.AccountId, ExpenseId = data.ExpenseId, CostCodeId = data.CodeId,
        TransactionType = "CashOut", Amount = amount, ReferenceNumber = Guid.NewGuid().ToString(), Description = "Expense"
    };
}
