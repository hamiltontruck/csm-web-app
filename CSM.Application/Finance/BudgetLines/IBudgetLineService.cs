using CSM.Application.Finance.BudgetLines.Dtos;

namespace CSM.Application.Finance.BudgetLines;

public interface IBudgetLineService
{
    Task<IReadOnlyCollection<BudgetLineResponse>> GetByBudgetAsync(
        Guid currentUserId,
        Guid budgetId,
        CancellationToken cancellationToken = default);

    Task<BudgetLineResponse> GetByIdAsync(
        Guid currentUserId,
        Guid budgetLineId,
        CancellationToken cancellationToken = default);

    Task<BudgetLineResponse> CreateAsync(
        Guid currentUserId,
        CreateBudgetLineRequest request,
        CancellationToken cancellationToken = default);

    Task<BudgetLineResponse> UpdateAsync(
        Guid currentUserId,
        Guid budgetLineId,
        UpdateBudgetLineRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid currentUserId,
        Guid budgetLineId,
        CancellationToken cancellationToken = default);
}
