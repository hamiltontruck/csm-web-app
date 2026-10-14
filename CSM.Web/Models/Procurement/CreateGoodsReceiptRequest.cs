namespace CSM.Web.Models.Procurement;

public class CreateGoodsReceiptRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? Remarks { get; set; }

    public List<GoodsReceiptLineRequest> Lines { get; set; } = new();

    public string? VehiclePlateNumber { get; set; }
}

public class GoodsReceiptLineRequest
{
    public Guid PurchaseOrderLineId { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public string? RejectionReason { get; set; }

    public string? Remarks { get; set; }
}