using CSM.Domain.Enums;

namespace CSM.Application.Inventory.StockMovements.Dtos;

public sealed class StockMovementResponse
{
    public Guid Id { get; set; }

    public Guid StockItemId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public StockMovementType MovementType { get; set; }

    public DateTime MovementDateUtc { get; set; }

    public decimal Quantity { get; set; }

    public decimal? StockBalanceBefore { get; set; }

    public decimal? StockBalanceAfter { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? TotalCost { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Remarks { get; set; }

    public string? MoveFrom { get; set; }
    public string? MoveTo { get; set; }
}

