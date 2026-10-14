namespace CSM.Web.Models.Procurement;

public class StockTransferResponse
{
    public Guid Id { get; set; }

    public Guid FromSiteId { get; set; }
    public Guid ToSiteId { get; set; }

    public string FromSiteName { get; set; } = "";
    public string ToSiteName { get; set; } = "";

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";

    public decimal Quantity { get; set; }

    public DateTime TransferDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = "";

    public string Status { get; set; } = "";

    public string? Remarks { get; set; }

    public Guid ConstructionSiteId { get; set; }
    public string SiteName { get; set; } = "";
}