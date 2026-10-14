namespace CSM.Application.Inventory.DailyMaterialUsage.Dtos;

public sealed class CreateDailyMaterialUsageRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid MaterialId { get; set; }

    public Guid DailyProgressLogId { get; set; }

    public DateOnly Date { get; set; }

    public decimal QuantityIssued { get; set; }

    public decimal QuantityUsed { get; set; }

    public decimal QuantityReturned { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
