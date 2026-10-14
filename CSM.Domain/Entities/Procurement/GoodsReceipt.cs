using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Procurement;

public class GoodsReceipt : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceivedAtUtc { get; set; }

    public Guid ReceivedBy { get; set; }

    public string? DeliveryNoteNumber { get; set; }

    public string? VehiclePlateNumber { get; set; }

    public string? Remarks { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public ICollection<GoodsReceiptLine> Lines { get; set; }
        = new List<GoodsReceiptLine>();
}