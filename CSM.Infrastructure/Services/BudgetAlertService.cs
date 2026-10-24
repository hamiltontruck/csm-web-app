using CSM.Application.Finance.Budgets;
using CSM.Application.Notifications;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class BudgetAlertService : IBudgetAlertService
{
    private const decimal WarningRatio = 0.8m;

    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationPublisher _notifications;

    public BudgetAlertService(
        ApplicationDbContext dbContext,
        INotificationPublisher notifications)
    {
        _dbContext = dbContext;
        _notifications = notifications;
    }

    public async Task StageForApprovedExpenseAsync(
        Expense expense,
        CancellationToken cancellationToken = default)
    {
        // Budgets in force that cover this expense.
        var budgets = await _dbContext.Budgets
            .AsNoTracking()
            .Where(b =>
                b.CompanyId == expense.CompanyId &&
                !b.IsDeleted &&
                (b.Status == BudgetStatus.Approved ||
                 b.Status == BudgetStatus.Active) &&
                b.CurrencyCode == expense.CurrencyCode &&
                (b.ConstructionSiteId == null ||
                 b.ConstructionSiteId == expense.ConstructionSiteId) &&
                (b.ProjectId == null ||
                 b.ProjectId == expense.ProjectId) &&
                (b.EffectiveFrom == null ||
                 b.EffectiveFrom <= expense.ExpenseDate) &&
                (b.EffectiveTo == null ||
                 b.EffectiveTo >= expense.ExpenseDate))
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
        {
            return;
        }

        var budgetIds = budgets.Select(b => b.Id).ToList();

        // Only the cost code line this expense belongs to can have changed.
        var lines = await _dbContext.BudgetLines
            .AsNoTracking()
            .Include(l => l.CostCode)
            .Where(l =>
                budgetIds.Contains(l.BudgetId) &&
                l.CostCodeId == expense.CostCodeId &&
                !l.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var budget in budgets)
        {
            // Whole budget.
            await CheckAsync(
                budget,
                expense,
                costCodeId: null,
                scope: "Budget",
                limit: budget.TotalAmount,
                cancellationToken);

            // This expense's cost code line.
            foreach (var line in lines.Where(l => l.BudgetId == budget.Id))
            {
                // Revised amount is the current limit when one was set.
                var limit = line.RevisedAmount > 0
                    ? line.RevisedAmount
                    : line.BudgetedAmount;

                await CheckAsync(
                    budget,
                    expense,
                    costCodeId: line.CostCodeId,
                    scope: $"Cost code {line.CostCode.Code}",
                    limit: limit,
                    cancellationToken);
            }
        }
    }

    private async Task CheckAsync(
        Budget budget,
        Expense expense,
        Guid? costCodeId,
        string scope,
        decimal limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return;
        }

        var before = await SpentAsync(
            budget,
            costCodeId,
            expense.Id,
            cancellationToken);

        var after = before + expense.Amount + expense.TaxAmount;

        var reachedFull = before < limit && after >= limit;
        var reachedWarning = before < limit * WarningRatio &&
                             after >= limit * WarningRatio;

        if (!reachedFull && !reachedWarning)
        {
            return;
        }

        var percent = after / limit * 100m;

        var message =
            $"{budget.BudgetNumber} - {budget.Name} / {scope}: " +
            $"{percent:0}% used " +
            $"({after:N2} of {limit:N2} {budget.CurrencyCode})";

        if (reachedFull)
        {
            // Everyone who can see the budget should know it is used up.
            await _notifications.StageForPermissionHoldersAsync(
                budget.CompanyId,
                budget.ConstructionSiteId,
                GranularPermission.Finance_Budget_View,
                excludeUserId: null,
                title: after > limit
                    ? "Budget exceeded"
                    : "Budget fully used",
                message: message,
                severity: NotificationSeverity.Critical,
                sourceEntityType: "Budget",
                sourceEntityId: budget.Id,
                cancellationToken: cancellationToken);
        }
        else
        {
            // 80%: the people who can still act on the budget.
            await _notifications.StageForPermissionHoldersAsync(
                budget.CompanyId,
                budget.ConstructionSiteId,
                GranularPermission.Finance_Budget_Manage,
                excludeUserId: null,
                title: "Budget 80% used",
                message: message,
                severity: NotificationSeverity.Warning,
                sourceEntityType: "Budget",
                sourceEntityId: budget.Id,
                cancellationToken: cancellationToken);
        }
    }

    // Approved and paid expenses under this budget, excluding the expense
    // being approved right now (it is not saved yet).
    private async Task<decimal> SpentAsync(
        Budget budget,
        Guid? costCodeId,
        Guid excludeExpenseId,
        CancellationToken cancellationToken)
    {
        var spent = await _dbContext.Expenses
            .AsNoTracking()
            .Where(e =>
                e.CompanyId == budget.CompanyId &&
                e.Id != excludeExpenseId &&
                !e.IsDeleted &&
                (e.Status == ExpenseStatus.Approved ||
                 e.Status == ExpenseStatus.Paid) &&
                e.CurrencyCode == budget.CurrencyCode &&
                (budget.ConstructionSiteId == null ||
                 e.ConstructionSiteId == budget.ConstructionSiteId) &&
                (budget.ProjectId == null ||
                 e.ProjectId == budget.ProjectId) &&
                (budget.EffectiveFrom == null ||
                 e.ExpenseDate >= budget.EffectiveFrom) &&
                (budget.EffectiveTo == null ||
                 e.ExpenseDate <= budget.EffectiveTo) &&
                (costCodeId == null ||
                 e.CostCodeId == costCodeId))
            .SumAsync(e => (decimal?)(e.Amount + e.TaxAmount), cancellationToken);

        return spent ?? 0m;
    }
}
