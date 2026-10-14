using CSM.Domain.Common;
using CSM.Domain.Entities.Finance;

namespace CSM.Domain.Entities.Procurement;

public class PurchaseOrderLine : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }

    public Guid MaterialId { get; set; }

    public Guid? CostCodeId { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Material Material { get; set; } = null!;

    public CostCode? CostCode { get; set; }
}