using CSM.Application.Inventory.StockTransfers.Dtos;


namespace CSM.Application.Inventory.StockTransfers;

public interface IStockTransferService
{
    Task<List<StockTransferResponse>> GetAllForUserAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<StockTransferResponse> CreateForUserAsync(Guid currentUserId, CreateStockTransferRequest request, CancellationToken cancellationToken = default);

    Task<List<StockTransferResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<StockTransferResponse> CreateAsync(
        Guid currentUserId,
        CreateStockTransferRequest request,
        CancellationToken cancellationToken = default);
}
