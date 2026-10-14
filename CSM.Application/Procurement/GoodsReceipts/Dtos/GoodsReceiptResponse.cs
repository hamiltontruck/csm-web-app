namespace CSM.Application.Procurement.GoodsReceipts.Dtos;

public sealed class GoodsReceiptResponse
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; }

    public Guid ReceivedBy { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? VehiclePlateNumber { get; set; }

    public string? Remarks { get; set; }

    public IReadOnlyCollection<GoodsReceiptLineResponse> Lines { get; set; }
        = [];
}
