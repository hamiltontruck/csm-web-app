namespace CSM.Application.Inventory.StockAdjustments.Dtos;

public sealed class StockAdjustmentResponse
{
    public Guid MovementId { get; set; }

    public Guid StockItemId { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public Guid ConstructionSiteId { get; set; }

    public string ConstructionSiteName { get; set; } = string.Empty;

    public bool Increase { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal QuantityBefore { get; set; }

    public decimal QuantityAfter { get; set; }

    public DateTime AdjustmentDateUtc { get; set; }

    public string AdjustmentNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? ApprovedBy { get; set; }

    public string? Remarks { get; set; }
}
