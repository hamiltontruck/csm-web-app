using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed partial class ExpenseSettlementPostgresTests
{
    [Fact]
    public async Task Concurrent_completions_recheck_remaining_under_the_expense_lock()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync();
        var data = await SeedAsync(fixture);
        Guid first, second;
        await using (var db = fixture.Context())
        {
            var service = new PaymentService(db);
            first = (await service.CreateAsync(data.ActorId, Request(data, 40m))).Id;
            second = (await service.CreateAsync(data.ActorId, Request(data, 40m))).Id;
        }
        await using var one = fixture.Context(); await using var two = fixture.Context();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => new PaymentService(one).CompletePaymentAsync(data.ActorId, first)),
            Record.ExceptionAsync(() => new PaymentService(two).ChangeStatusAsync(data.ActorId, second, new(PaymentStatus.Completed))));
        Assert.Single(errors, x => x is null);
        await using var verify = fixture.Context();
        Assert.Equal(1, await verify.Payments.CountAsync(x => x.Status == PaymentStatus.Completed));
        Assert.Equal(1, await verify.Payments.CountAsync(x => x.Status == PaymentStatus.Pending));
        var expense = await verify.Expenses.SingleAsync(); Assert.Equal(ExpenseStatus.Approved, expense.Status);
        Assert.Equal(40m, (await new ExpenseSettlementService(verify).GetSummaryAsync(expense, default)).SettledAmount);
    }

    [Fact]
    public async Task Failure_after_payment_save_rolls_back_payment_and_expense_together()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture); Guid id;
        await using (var db = fixture.Context()) id = (await new PaymentService(db).CreateAsync(data.ActorId, Request(data, 60m))).Id;
        await using (var fail = new FailingSettlementDb(fixture.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PaymentService(fail).CompletePaymentAsync(data.ActorId, id));
        await using var verify = fixture.Context();
        Assert.Equal(PaymentStatus.Pending, (await verify.Payments.SingleAsync()).Status);
        Assert.Equal(ExpenseStatus.Approved, (await verify.Expenses.SingleAsync()).Status);
    }

    [Fact]
    public async Task Duplicate_pending_number_does_not_create_second_evidence()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync(); var data = await SeedAsync(fixture);
        var request = Request(data, 20m);
        await using var one = fixture.Context(); await using var two = fixture.Context();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => new PaymentService(one).CreateAsync(data.ActorId, request)),
            Record.ExceptionAsync(() => new PaymentService(two).CreateAsync(data.ActorId, request)));
        Assert.Single(errors, x => x is null);
        await using var verify = fixture.Context(); Assert.Single(await verify.Payments.ToListAsync());
        Assert.Equal(ExpenseStatus.Approved, (await verify.Expenses.SingleAsync()).Status);
    }

    private sealed class FailingSettlementDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<Expense>().Any(x => x.State == EntityState.Modified && x.Entity.Status == ExpenseStatus.Paid))
                throw new InvalidOperationException("Injected settlement save failure.");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record Seed(Guid ActorId, Guid CompanyId, Guid SiteId, Guid CodeId, Guid ExpenseId, Guid AccountId);
    private static async Task<Seed> SeedAsync(FinanceSettlementPostgresFixture fixture)
    {
        var actor = FinanceOfficerAuthorityTests.Actor();
        var company = new Company { Id = actor.CompanyId!.Value, Code = "CO", Name = "Company", IsActive = true };
        var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "SITE", Name = "Assigned", IsActive = true };
        var code = new CostCode { CompanyId = company.Id, ConstructionSiteId = site.Id, Code = "COST", Name = "Cost", IsActive = true };
        var expense = new Expense { CompanyId = company.Id, ConstructionSiteId = site.Id, CostCodeId = code.Id, ExpenseNumber = "EXP", Description = "Expense", Amount = 50m, TaxAmount = 10m, Status = ExpenseStatus.Approved, CreatedBy = actor.Id, UpdatedBy = actor.Id, ApprovedBy = Guid.NewGuid() };
        var account = new PettyCashAccount { CompanyId = company.Id, ConstructionSiteId = site.Id, AccountCode = "CASH", AccountName = "Cash", OpeningBalance = 100m, CurrentBalance = 100m, IsActive = true };
        await using var db = fixture.Context(); db.Companies.Add(company); db.Users.Add(actor); db.ConstructionSites.Add(site); db.CostCodes.Add(code); db.Expenses.Add(expense); db.PettyCashAccounts.Add(account);
        db.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company.Id, SiteId = site.Id, IsActive = true }); await db.SaveChangesAsync();
        return new(actor.Id, company.Id, site.Id, code.Id, expense.Id, account.Id);
    }
    private static CreatePaymentRequest Request(Seed data, decimal amount) => new(data.SiteId, null, null, null, null,
        Guid.NewGuid().ToString(), DateOnly.FromDateTime(DateTime.UtcNow), PaymentDirection.Outgoing, amount, "ETB", 1m, "Cash", "REF", "Expense") { ExpenseId = data.ExpenseId };
}
