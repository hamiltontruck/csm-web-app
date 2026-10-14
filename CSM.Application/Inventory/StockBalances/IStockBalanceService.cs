using CSM.Application.Inventory.StockBalances.Dtos;

namespace CSM.Application.Inventory.StockBalances;

public interface IStockBalanceService
{
    Task<IReadOnlyCollection<StockBalanceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? materialId = null,
        CancellationToken cancellationToken = default);

    Task<StockBalanceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockItemId,
        CancellationToken cancellationToken = default);
}
