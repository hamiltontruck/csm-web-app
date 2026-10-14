namespace CSM.Application.Inventory.StockAdjustments.Dtos;

public sealed class CreateStockAdjustmentRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid MaterialId { get; set; }

    public bool Increase { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public DateTime AdjustmentDateUtc { get; set; }

    public string AdjustmentNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? ApprovedBy { get; set; }

    public string? Remarks { get; set; }
}
