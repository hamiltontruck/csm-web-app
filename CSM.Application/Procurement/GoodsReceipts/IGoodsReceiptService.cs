using CSM.Application.Procurement.GoodsReceipts.Dtos;

namespace CSM.Application.Procurement.GoodsReceipts;

public interface IGoodsReceiptService
{
    Task<IReadOnlyCollection<GoodsReceiptResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? purchaseOrderId = null,
        CancellationToken cancellationToken = default);

    Task<GoodsReceiptResponse> GetByIdAsync(
        Guid currentUserId,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default);

    Task<GoodsReceiptResponse> CreateAsync(
        Guid currentUserId,
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken = default);
}
