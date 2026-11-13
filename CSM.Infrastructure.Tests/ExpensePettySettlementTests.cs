using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.PettyCashTransactions.Dtos;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class ExpensePettySettlementTests
{
    [Fact]
    public async Task Partial_cash_and_payment_settle_together_and_payment_reversal_preserves_cash_evidence()
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        f.Account.CurrentBalance = 100m; await f.Db.SaveChangesAsync();
        var cash = await new PettyCashTransactionService(f.Db).CreateAsync(f.Actor.Id, Request(f, 20m));
        Assert.Equal(80m, cash.BalanceAfter); Assert.Equal(80m, f.Account.CurrentBalance);
        Assert.Equal(40m, (await new ExpenseSettlementService(f.Db).GetSummaryAsync(f.Expense, default)).OutstandingAmount);
        var payments = new PaymentService(f.Db);
        var payment = await payments.CreateAsync(f.Actor.Id, ExpensePaymentSettlementTests.Request(f, 40m));
        await payments.CompletePaymentAsync(f.Actor.Id, payment.Id); Assert.Equal(ExpenseStatus.Paid, f.Expense.Status);
        await payments.ReversePaymentAsync(f.Actor.Id, payment.Id); Assert.Equal(ExpenseStatus.Approved, f.Expense.Status);
        Assert.Equal(40m, (await new ExpenseSettlementService(f.Db).GetSummaryAsync(f.Expense, default)).OutstandingAmount);
        Assert.Equal(80m, f.Account.CurrentBalance);
    }

    [Theory]
    [InlineData("cash-in")] [InlineData("unknown-type")] [InlineData("wrong-code")] [InlineData("wrong-site")]
    [InlineData("usd")] [InlineData("rate")] [InlineData("inactive")]
    [InlineData("overdraft")] [InlineData("overpay")] [InlineData("precision")] [InlineData("empty-ref")]
    public async Task Invalid_cash_out_cannot_change_account_or_expense(string kind)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture(); var request = Request(f, 20m);
        switch(kind)
        {
            case "cash-in": request.TransactionType = "CashIn"; break;
            case "unknown-type": request.TransactionType = "Anything"; break;
            case "wrong-code": request.CostCodeId = Guid.NewGuid(); break;
            case "wrong-site": f.Account.ConstructionSiteId = Guid.NewGuid(); break;
            case "usd": f.Expense.CurrencyCode = "USD"; break;
            case "rate": f.Expense.ExchangeRate = 2m; break;
            case "inactive": f.Account.IsActive = false; break;
            case "overdraft": f.Account.CurrentBalance = 10m; break;
            case "overpay": request.Amount = 61m; break;
            case "precision": request.Amount = 0.00001m; break;
            case "empty-ref": request.ReferenceNumber = " "; break;
        }
        await f.Db.SaveChangesAsync(); var before = f.Account.CurrentBalance;
        await Assert.ThrowsAsync<PettyCashManagementException>(() => new PettyCashTransactionService(f.Db).CreateAsync(f.Actor.Id, request));
        Assert.Equal(before, f.Account.CurrentBalance); Assert.Empty(f.Db.PettyCashTransactions); Assert.Equal(ExpenseStatus.Approved, f.Expense.Status);
    }

    [Fact]
    public async Task Replayed_partial_reference_never_debits_twice()
    {
        using var f = new ExpenseSettlementRulesTests.Fixture(); var request = Request(f, 20m); var service = new PettyCashTransactionService(f.Db);
        await service.CreateAsync(f.Actor.Id, request);
        await Assert.ThrowsAsync<PettyCashManagementException>(() => service.CreateAsync(f.Actor.Id, request));
        Assert.Equal(180m, f.Account.CurrentBalance); Assert.Single(f.Db.PettyCashTransactions);
    }

    [Theory]
    [InlineData("cashout", "CashOut")] [InlineData(" cashin ", "CashIn")]
    public async Task Existing_unlinked_types_are_canonicalized(string input, string expected)
    {
        using var f = new ExpenseSettlementRulesTests.Fixture();
        var response = await new PettyCashTransactionService(f.Db).CreateAsync(f.Actor.Id, new() { PettyCashAccountId = f.Account.Id, TransactionType = input, ReferenceNumber = "UNLINKED", Amount = 10m, Description = "Direct" });
        Assert.Equal(expected, response.TransactionType);
    }

    internal static CreatePettyCashTransactionRequest Request(ExpenseSettlementRulesTests.Fixture f, decimal amount)
    {
        var request = new CreatePettyCashTransactionRequest { PettyCashAccountId = f.Account.Id, CostCodeId = f.Expense.CostCodeId, TransactionType = "CashOut", Amount = amount, ReferenceNumber = Guid.NewGuid().ToString(), Description = "Expense" };
        var link = typeof(CreatePettyCashTransactionRequest).GetProperty("ExpenseId"); Assert.NotNull(link); link.SetValue(request, f.Expense.Id); return request;
    }
}
