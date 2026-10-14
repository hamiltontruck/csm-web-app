using CSM.Domain.Common;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Finance;

namespace CSM.Domain.Entities.Procurement;

public class Material : TenantEntity
{
    public string MaterialCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public MaterialType MaterialType { get; set; } = MaterialType.Bulk;

    public Guid? DefaultCostCodeId { get; set; }

    public decimal? StandardUnitCost { get; set; }

    public bool IsActive { get; set; } = true;

    public CostCode? DefaultCostCode { get; set; }

    public RebarSpec? RebarSpec { get; set; }

    public ICollection<StockItem> StockItems { get; set; }
        = new List<StockItem>();

    public ICollection<MaterialRequestLine> MaterialRequestLines { get; set; }
        = new List<MaterialRequestLine>();

    public ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        = new List<PurchaseOrderLine>();
}

