using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Procurement;

public class StockMovement : TenantEntity
{
    public Guid StockItemId { get; set; }

    public StockMovementType MovementType { get; set; }

    public DateTime MovementDateUtc { get; set; } = DateTime.UtcNow;

    public decimal Quantity { get; set; }

    public decimal? StockBalanceBefore { get; set; }

    public decimal? StockBalanceAfter { get; set; }

    public decimal? UnitCost { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Remarks { get; set; }

    public StockItem StockItem { get; set; } = null!;
}

