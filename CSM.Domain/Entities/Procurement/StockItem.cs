using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Procurement;

public class StockItem : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid MaterialId { get; set; }

    public decimal QuantityOnHand { get; set; }

    public decimal QuantityReserved { get; set; }

    public decimal ReorderLevel { get; set; }

    public decimal? MaximumStockLevel { get; set; }

    public decimal AverageUnitCost { get; set; }

    public string? StorageLocation { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Material Material { get; set; } = null!;

    public ICollection<StockMovement> Movements { get; set; }
        = new List<StockMovement>();
}