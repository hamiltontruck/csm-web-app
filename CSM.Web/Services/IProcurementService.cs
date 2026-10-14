using CSM.Web.Models.Procurement;

namespace CSM.Web.Services;

public interface IProcurementService
{
    Task<List<MaterialResponse>> GetMaterialsAsync(
        bool? isActive = null,
        string? category = null);

    Task<MaterialResponse?> GetMaterialAsync(Guid id);

    Task<bool> CreateMaterialAsync(CreateMaterialRequest request);

    Task<bool> UpdateMaterialAsync(
        Guid id,
        UpdateMaterialRequest request);

    Task<List<MaterialLookupDto>> GetCategoriesAsync();
    Task<List<MaterialLookupDto>> GetUnitsAsync();
    Task<List<MaterialLookupDto>> GetMaterialTypesAsync();
    Task<List<StockItemResponse>> GetStockItemsAsync();

    Task<List<MaterialRequestResponse>> GetMaterialRequestsAsync();
    Task CreateMaterialRequestAsync(CreateMaterialRequestRequest request);

    Task<MaterialRequestResponse?> GetMaterialRequestAsync(Guid id);

    Task<List<PurchaseOrderResponse>> GetPurchaseOrdersAsync();
    Task<PurchaseOrderResponse?> GetPurchaseOrderAsync(Guid id);
    Task CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request);

    Task<List<SupplierResponse>> GetVendorsAsync();

    Task<List<GoodsReceiptResponse>> GetGoodsReceiptsAsync();
    Task<GoodsReceiptResponse?> GetGoodsReceiptAsync(Guid id);
    Task CreateGoodsReceiptAsync(CreateGoodsReceiptRequest request);

    Task SendPurchaseOrderToVendorAsync(Guid purchaseOrderId);

    Task ApprovePurchaseOrderAsync(Guid purchaseOrderId);

    Task SubmitPurchaseOrderForApprovalAsync(Guid purchaseOrderId);

    Task<List<PurchaseOrderResponse>> GetReceivablePurchaseOrdersAsync();


}