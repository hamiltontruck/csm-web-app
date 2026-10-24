using CSM.Domain.Entities.Finance;

namespace CSM.Application.Finance.Budgets;

public interface IBudgetAlertService
{
    /// <summary>
    /// Call when an expense is being approved, BEFORE the caller saves.
    /// Works out whether this expense moves any budget (or its cost code
    /// line) across 80% or 100% used, and stages notifications for it.
    /// Only stages; the caller saves, so the alert is committed together
    /// with the approval.
    /// </summary>
    Task StageForApprovedExpenseAsync(
        Expense expense,
        CancellationToken cancellationToken = default);
}
