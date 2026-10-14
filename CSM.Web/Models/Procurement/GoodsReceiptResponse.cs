namespace CSM.Web.Models.Procurement;

public class GoodsReceiptResponse
{
    public Guid Id { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid PurchaseOrderId { get; set; }

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; }

    public Guid ReceivedBy { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? VehiclePlateNumber { get; set; }

    public string? Remarks { get; set; }

    public List<GoodsReceiptLineResponse> Lines { get; set; } = new();
}

public class GoodsReceiptLineResponse
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderLineId { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public decimal ReceivedQuantity { get; set; }

    // NEW
    public decimal OrderedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public decimal? StockBalanceBefore { get; set; }

    public decimal? StockBalanceAfter { get; set; }

    public string? RejectionReason { get; set; }

    public string? Remarks { get; set; }
}