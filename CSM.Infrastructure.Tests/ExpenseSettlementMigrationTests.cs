using CSM.Infrastructure.Persistence;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class ExpenseSettlementMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_legacy_paid_and_unlinked_duplicate_references_and_enforces_new_links()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync("20261111142950_AddAccountLoginHistory");
        var actor = FinanceOfficerAuthorityTests.Actor();
        var company = new Company { Id = actor.CompanyId!.Value, Code = "CO", Name = "Company", IsActive = true };
        var code = new CostCode { CompanyId = company.Id, Code = "COST", Name = "Cost", IsActive = true };
        var account = new PettyCashAccount { CompanyId = company.Id, AccountCode = "CASH", AccountName = "Cash", CurrentBalance = 40m };
        var expense = new Expense { CompanyId = company.Id, CostCodeId = code.Id, ExpenseNumber = "LEGACY", Description = "Historical paid", Amount = 50m, Status = ExpenseStatus.Paid };
        await using (var seed = new LegacySchemaDb(fixture.Options))
        {
            seed.Companies.Add(company); seed.Users.Add(actor); seed.CostCodes.Add(code); seed.PettyCashAccounts.Add(account); seed.Expenses.Add(expense);
            for (var i = 0; i < 2; i++) seed.PettyCashTransactions.Add(new() { PettyCashAccountId = account.Id, CreatedByUserId = actor.Id, ReferenceNumber = "DUPLICATE", TransactionType = "CashOut", Amount = 10m, Description = "Old unlinked", TransactionDateUtc = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }
        await using (var upgrade = fixture.Context())
        {
            await upgrade.Database.MigrateAsync();
            Assert.False(upgrade.Database.HasPendingModelChanges());
        }
        await using (var verify = fixture.Context())
        {
            var old = await verify.Expenses.SingleAsync(); Assert.Equal(ExpenseStatus.Paid, old.Status); Assert.Null(old.CreatedBy);
            var summary = await new ExpenseSettlementService(verify).GetSummaryAsync(old, CancellationToken.None);
            Assert.True(summary.IsLegacyPaid); Assert.Null(summary.OutstandingAmount);
            Assert.Equal(2, await verify.PettyCashTransactions.CountAsync(x => x.ReferenceNumber == "DUPLICATE" && x.ExpenseId == null));
            Assert.Equal(40m, (await verify.PettyCashAccounts.SingleAsync()).CurrentBalance);
            verify.PettyCashTransactions.Add(new() { PettyCashAccountId = account.Id, CreatedByUserId = actor.Id, ReferenceNumber = "LINKED", TransactionType = "CashOut", ExpenseId = expense.Id, Amount = 1m, Description = "Linked", TransactionDateUtc = DateTime.UtcNow });
            await verify.SaveChangesAsync();
        }
        await using (var duplicate = fixture.Context())
        {
            duplicate.PettyCashTransactions.Add(new() { PettyCashAccountId = account.Id, CreatedByUserId = actor.Id, ReferenceNumber = "LINKED", TransactionType = "CashOut", ExpenseId = expense.Id, Amount = 1m, Description = "Duplicate", TransactionDateUtc = DateTime.UtcNow });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        await using (var constraints = fixture.Context())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => constraints.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Expenses\" WHERE \"Id\" = {expense.Id}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
            constraints.Payments.Add(new() { CompanyId = company.Id, ExpenseId = Guid.NewGuid(), PaymentNumber = "INVALID", Amount = 1m });
            var missing = await Assert.ThrowsAsync<DbUpdateException>(() => constraints.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(missing.InnerException).SqlState);
        }
        await using (var linked = fixture.Context())
        {
            linked.Payments.Add(new() { CompanyId = company.Id, ExpenseId = expense.Id, PaymentNumber = "LINKED-PAY", Direction = PaymentDirection.Outgoing, Status = PaymentStatus.Completed, Amount = 1m });
            await linked.SaveChangesAsync();
        }
        await using (var down = fixture.Context())
        {
            var failure = await Assert.ThrowsAsync<NotSupportedException>(() => down.GetService<IMigrator>().MigrateAsync("20261111142950_AddAccountLoginHistory"));
            Assert.Contains("retain", failure.Message, StringComparison.OrdinalIgnoreCase);
        }
        await using (var retained = fixture.Context())
        {
            Assert.Equal(expense.Id, (await retained.Payments.SingleAsync()).ExpenseId);
            Assert.Equal(1, await retained.PettyCashTransactions.CountAsync(x => x.ExpenseId == expense.Id));
            Assert.Contains("20261113014528_LinkExpenseSettlementEvidence", await retained.Database.GetAppliedMigrationsAsync());
            Assert.True(await retained.Database.SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM pg_indexes WHERE indexname = 'IX_PettyCashTransactions_PettyCashAccountId_ReferenceNumber') AS \"Value\"").SingleAsync());
        }
    }
    // Seed the actual prior schema directly, without new evidence columns or links.
    private sealed class LegacySchemaDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            var payments = builder.Entity<Payment>();
            payments.Ignore(x => x.Expense);
            foreach (var fk in payments.Metadata.GetForeignKeys().Where(x => x.Properties.Any(p => p.Name == "ExpenseId")).ToArray()) payments.Metadata.RemoveForeignKey(fk);
            payments.Ignore(x => x.ExpenseId);
            var cash = builder.Entity<PettyCashTransaction>();
            cash.Ignore(x => x.Expense);
            foreach (var fk in cash.Metadata.GetForeignKeys().Where(x => x.Properties.Any(p => p.Name == "ExpenseId")).ToArray()) cash.Metadata.RemoveForeignKey(fk);
            cash.Ignore(x => x.ExpenseId);
        }
    }

}
