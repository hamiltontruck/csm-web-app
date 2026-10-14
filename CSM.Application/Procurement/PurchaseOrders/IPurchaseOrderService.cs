using CSM.Application.Procurement.PurchaseOrders.Dtos;
using CSM.Domain.Enums;

namespace CSM.Application.Procurement.PurchaseOrders;

public interface IPurchaseOrderService
{
    Task<IReadOnlyCollection<PurchaseOrderResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId,
        Guid? vendorId,
        PurchaseOrderStatus? status,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> GetByIdAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> CreateAsync(
        Guid currentUserId,
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> UpdateAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> SubmitAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> ApproveAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> SendToVendorAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> CancelAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);
}
