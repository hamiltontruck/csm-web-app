namespace CSM.Web.Models.Procurement;

public class StockAdjustmentResponse
{
    public Guid MovementId { get; set; }

    public Guid StockItemId { get; set; }

    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = "";

    public bool Increase { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }

    public DateTime AdjustmentDateUtc { get; set; }

    public string AdjustmentNumber { get; set; } = "";

    public string Reason { get; set; } = "";

    public string? ApprovedBy { get; set; }
    public string? Remarks { get; set; }
}