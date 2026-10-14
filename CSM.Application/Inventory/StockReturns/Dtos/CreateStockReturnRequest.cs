namespace CSM.Application.Inventory.StockReturns.Dtos;

public sealed class CreateStockReturnRequest
{
    public Guid OriginalIssueMovementId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime ReturnDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string? ReturnedBy { get; set; }

    public string? Reason { get; set; }

    public string? Remarks { get; set; }
}
