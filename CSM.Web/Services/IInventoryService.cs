using CSM.Web.Models.Procurement;

namespace CSM.Web.Services;

public interface IInventoryService
{
    Task<List<CSM.Web.Models.Procurement.StockBalanceResponse>>
        GetStockBalancesAsync(
            Guid? constructionSiteId = null,
            Guid? materialId = null);

    Task<StockBalanceResponse?> GetStockBalanceAsync(
        Guid constructionSiteId,
        Guid materialId);

    Task<List<StockMovementResponse>> GetStockMovementsAsync(
        Guid? constructionSiteId = null,
        Guid? materialId = null);

    Task<StockMovementResponse?> GetStockMovementAsync(Guid id);

    Task<List<StockBalanceResponse>> GetStockBySiteAsync(Guid siteId);

    Task<bool> CreateStockTransferAsync(StockTransferCreateRequest request);

    Task<List<StockTransferResponse>> GetStockTransfersAsync();

    Task<bool> CreateStockAdjustmentAsync(
        StockAdjustmentCreateRequest request);

        Task<List<StockAdjustmentResponse>> GetStockAdjustmentsAsync();

        Task<List<StockIssueResponse>> GetStockIssuesAsync();

        Task<bool> CreateStockIssueAsync(
            CreateStockIssueRequest request);

            Task<bool> CreateDailyMaterialUsageAsync(
                CreateDailyMaterialUsageRequest request);

        Task<List<DailyMaterialUsageResponse>> GetDailyMaterialUsageAsync();

        Task<List<StockReturnResponse>> GetStockReturnsAsync();

        Task<bool> CreateStockReturnAsync(CreateStockReturnRequest request);
}