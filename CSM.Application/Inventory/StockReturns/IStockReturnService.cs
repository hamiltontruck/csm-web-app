using CSM.Application.Inventory.StockReturns.Dtos;

namespace CSM.Application.Inventory.StockReturns;

public interface IStockReturnService
{
    Task<IReadOnlyCollection<StockReturnResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<StockReturnResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockReturnId,
        CancellationToken cancellationToken = default);

    Task<StockReturnResponse> CreateAsync(
        Guid currentUserId,
        CreateStockReturnRequest request,
        CancellationToken cancellationToken = default);
}
