namespace CSM.Application.Inventory.StockTransfers.Dtos;

public class StockTransferResponse
{
    public Guid Id { get; set; }

    // Transfer movements
    public Guid TransferOutMovementId { get; set; }
    public Guid TransferInMovementId { get; set; }

    // Site information
    public Guid SourceConstructionSiteId { get; set; }
    public Guid DestinationConstructionSiteId { get; set; }

    public string SourceSiteName { get; set; } = "";
    public string DestinationSiteName { get; set; } = "";

    // Aliases used by the Web project
    public Guid FromSiteId { get; set; }
    public Guid ToSiteId { get; set; }

    public string FromSiteName { get; set; } = "";
    public string ToSiteName { get; set; } = "";

    // Material
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    // Transfer details
    public DateTime TransferDateUtc { get; set; }

    public string ReferenceNumber { get; set; } = "";
    public string TransferNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }

    public decimal SourceQuantityBefore { get; set; }
    public decimal SourceQuantityAfter { get; set; }

    public decimal DestinationQuantityBefore { get; set; }
    public decimal DestinationQuantityAfter { get; set; }
}