using CSM.Domain.Entities.Finance;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

internal sealed record FinanceMutationTargets(Guid? BudgetId = null,
    IReadOnlyCollection<Guid>? ExpenseIds = null, Guid? PaymentId = null, Guid? AccountId = null);

/// <summary>Execute one persisted finance mutation with retries and deterministic PostgreSQL row locks.</summary>
internal static class FinanceMutationTransaction
{
    public static async Task<T> ExecuteAsync<T>(ApplicationDbContext db, FinanceMutationTargets targets,
        Func<Task<T>> action, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return await action();
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Finance mutations require an independent persisted unit of work.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                if (targets.BudgetId is Guid budget)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Budgets\" WHERE \"Id\" = {budget} FOR UPDATE", ct);
                    await ReloadAsync<Budget>(db, x => x.Id == budget, ct);
                    await ReloadAsync<BudgetLine>(db, x => x.BudgetId == budget, ct);
                }
                foreach (var expense in (targets.ExpenseIds ?? []).Distinct().Order())
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Expenses\" WHERE \"Id\" = {expense} FOR UPDATE", ct);
                    await ReloadAsync<Expense>(db, x => x.Id == expense, ct);
                }
                if (targets.PaymentId is Guid payment)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Payments\" WHERE \"Id\" = {payment} FOR UPDATE", ct);
                    await ReloadAsync<Payment>(db, x => x.Id == payment, ct);
                }
                if (targets.AccountId is Guid account)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"PettyCashAccounts\" WHERE \"Id\" = {account} FOR UPDATE", ct);
                    await ReloadAsync<PettyCashAccount>(db, x => x.Id == account, ct);
                }
                var result = await action();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                // A retry must reload persisted state, including rows saved before a rolled-back second save.
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public static Task ExecuteAsync(ApplicationDbContext db, FinanceMutationTargets targets,
        Func<Task> action, CancellationToken ct) => ExecuteAsync<object?>(db, targets, async () => { await action(); return null; }, ct);

    private static async Task ReloadAsync<T>(ApplicationDbContext db, Func<T, bool> matches, CancellationToken ct) where T : class
    {
        foreach (var entry in db.ChangeTracker.Entries<T>().Where(x => matches(x.Entity)).ToArray())
            await entry.ReloadAsync(ct);
    }
}
