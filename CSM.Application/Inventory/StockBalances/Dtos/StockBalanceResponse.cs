namespace CSM.Application.Inventory.StockBalances.Dtos;

public sealed class StockBalanceResponse
{
    public Guid StockItemId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal QuantityOnHand { get; set; }

    public decimal QuantityReserved { get; set; }

    public decimal AvailableQuantity { get; set; }

    public decimal ReorderLevel { get; set; }

    public decimal? MaximumStockLevel { get; set; }

    public decimal AverageUnitCost { get; set; }

    public string? StorageLocation { get; set; }
}
