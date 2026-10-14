using CSM.Domain.Common;

namespace CSM.Domain.Entities.Procurement;

public class GoodsReceiptLine : TenantEntity
{
    public Guid GoodsReceiptId { get; set; }

    public Guid PurchaseOrderLineId { get; set; }

    public Guid MaterialId { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public string? RejectionReason { get; set; }

    public string? Remarks { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;

    public Material Material { get; set; } = null!;
}