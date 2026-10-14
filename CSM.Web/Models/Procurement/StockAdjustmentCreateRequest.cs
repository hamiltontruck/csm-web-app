namespace CSM.Web.Models.Procurement;

public class StockAdjustmentCreateRequest
{
    public Guid ConstructionSiteId { get; set; }
    public Guid MaterialId { get; set; }

    public bool Increase { get; set; }

    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }

    public DateTime AdjustmentDateUtc { get; set; }

    public string AdjustmentNumber { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public string? Remarks { get; set; }
}