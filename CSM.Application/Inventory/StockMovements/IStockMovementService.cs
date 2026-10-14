using CSM.Application.Inventory.StockMovements.Dtos;
using CSM.Domain.Enums;

namespace CSM.Application.Inventory.StockMovements;

public interface IStockMovementService
{
    Task<IReadOnlyCollection<StockMovementResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? materialId = null,
        StockMovementType? movementType = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<StockMovementResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockMovementId,
        CancellationToken cancellationToken = default);
}
