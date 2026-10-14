using CSM.Application.Inventory.StockAdjustments.Dtos;

namespace CSM.Application.Inventory.StockAdjustments;

public interface IStockAdjustmentService
{
    Task<StockAdjustmentResponse> CreateAsync(
        Guid currentUserId,
        CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<List<StockAdjustmentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
