namespace CSM.Application.Inventory.StockReturns.Dtos;

public sealed class StockReturnResponse
{
    public Guid Id { get; set; }

    public Guid OriginalIssueMovementId { get; set; }

    public Guid StockItemId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal OriginalIssuedQuantity { get; set; }

    public decimal PreviouslyReturnedQuantity { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public decimal RemainingReturnableQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal QuantityOnHandBefore { get; set; }

    public decimal QuantityOnHandAfter { get; set; }

    public DateTime ReturnDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string? ReturnedBy { get; set; }

    public string? Reason { get; set; }

    public string? Remarks { get; set; }
}
