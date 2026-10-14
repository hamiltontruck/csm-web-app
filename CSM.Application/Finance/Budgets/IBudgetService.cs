using CSM.Application.Finance.Budgets.Dtos;

namespace CSM.Application.Finance.Budgets;

public interface IBudgetService
{
    Task<IReadOnlyCollection<BudgetResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BudgetResponse> GetByIdAsync(
        Guid currentUserId,
        Guid budgetId,
        CancellationToken cancellationToken = default);

    Task<BudgetResponse> CreateAsync(
        Guid currentUserId,
        CreateBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<BudgetResponse> UpdateAsync(
        Guid currentUserId,
        Guid budgetId,
        UpdateBudgetRequest request,
        CancellationToken cancellationToken = default);

    Task<BudgetResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid budgetId,
        ChangeBudgetStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BudgetResponse>> GetBySiteAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default);
}
