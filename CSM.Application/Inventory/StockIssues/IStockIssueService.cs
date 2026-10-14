using CSM.Application.Inventory.StockIssues.Dtos;

namespace CSM.Application.Inventory.StockIssues;

public interface IStockIssueService
{
    Task<IReadOnlyCollection<StockIssueResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<StockIssueResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockMovementId,
        CancellationToken cancellationToken = default);

    Task<StockIssueResponse> CreateAsync(
        Guid currentUserId,
        CreateStockIssueRequest request,
        CancellationToken cancellationToken = default);
}
