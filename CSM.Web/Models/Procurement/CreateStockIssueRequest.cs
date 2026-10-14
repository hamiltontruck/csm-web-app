namespace CSM.Web.Models.Procurement;

public class CreateStockIssueRequest
{
    public Guid ConstructionSiteId { get; set; }
    public Guid MaterialId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime IssueDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = "";

    public string? IssuedTo { get; set; }
    public string? Purpose { get; set; }
    public string? Remarks { get; set; }
}