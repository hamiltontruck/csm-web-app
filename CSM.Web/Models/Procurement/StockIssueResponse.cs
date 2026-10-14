namespace CSM.Web.Models.Procurement;

public class StockIssueResponse
{
    public Guid MovementId { get; set; }

    public Guid StockItemId { get; set; }

    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = "";

    public decimal QuantityIssued { get; set; }

    public decimal QuantityOnHandBefore { get; set; }
    public decimal QuantityOnHandAfter { get; set; }

    public decimal UnitCost { get; set; }

    public DateTime IssueDateUtc { get; set; }

    public string IssueNumber { get; set; } = "";

    public string IssuedTo { get; set; } = "";
    public string? Remarks { get; set; }

    public string SiteName { get; set; } = "";

    public string ReferenceNumber { get; set; } = "";
}