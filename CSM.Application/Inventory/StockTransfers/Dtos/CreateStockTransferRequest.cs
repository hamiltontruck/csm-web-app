namespace CSM.Application.Inventory.StockTransfers.Dtos;

public sealed class CreateStockTransferRequest
{
    public Guid SourceConstructionSiteId { get; set; }

    public Guid DestinationConstructionSiteId { get; set; }

    public Guid MaterialId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime TransferDateUtc { get; set; }

    public string TransferNumber { get; set; } = string.Empty;

    public string? RequestedBy { get; set; }

    public string? ApprovedBy { get; set; }

    public string? Remarks { get; set; }
}
