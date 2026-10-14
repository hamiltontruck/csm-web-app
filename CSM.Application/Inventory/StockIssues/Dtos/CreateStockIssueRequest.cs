namespace CSM.Application.Inventory.StockIssues.Dtos;

public sealed class CreateStockIssueRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid MaterialId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime IssueDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string? IssuedTo { get; set; }

    public string? Purpose { get; set; }

    public string? Remarks { get; set; }
}
