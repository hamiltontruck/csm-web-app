namespace CSM.Application.Inventory.StockIssues.Dtos;

public sealed class StockIssueResponse
{
    public Guid StockMovementId { get; set; }

    public Guid StockItemId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal QuantityIssued { get; set; }

    public decimal QuantityOnHandBefore { get; set; }

    public decimal QuantityOnHandAfter { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime IssueDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string? IssuedTo { get; set; }

    public string? Purpose { get; set; }

    public string? Remarks { get; set; }
}
