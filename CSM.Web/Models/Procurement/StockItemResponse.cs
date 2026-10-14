namespace CSM.Web.Models.Procurement;

public class StockItemResponse
{
    public Guid Id { get; set; }

    public Guid MaterialId { get; set; }
    public Guid ConstructionSiteId { get; set; }

    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";

    public string SiteCode { get; set; } = "";
    public string SiteName { get; set; } = "";

    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal ReorderLevel { get; set; }

    public string UnitOfMeasure { get; set; } = "";
}