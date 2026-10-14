namespace CSM.Application.Procurement.GoodsReceipts.Dtos;

public sealed class CreateGoodsReceiptRequest
{
    public Guid PurchaseOrderId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? VehiclePlateNumber { get; set; }

    public string? Remarks { get; set; }

    public List<CreateGoodsReceiptLineRequest> Lines { get; set; }
        = [];
}
