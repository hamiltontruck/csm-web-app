namespace CSM.Application.Inventory.DailyMaterialUsage.Dtos;

public sealed class DailyMaterialUsageResponse
{
    public Guid Id { get; set; }

    public string UsageNumber { get; set; } = string.Empty;

    public Guid ConstructionSiteId { get; set; }

    public string ConstructionSiteName { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public Guid DailyProgressLogId { get; set; }

    public Guid LoggedByUserId { get; set; }

    public string LoggedByUserName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public decimal QuantityIssued { get; set; }

    public decimal QuantityUsed { get; set; }

    public decimal QuantityReturned { get; set; }

    public decimal QuantityVariance { get; set; }
    public bool RequiresSupervisorReview { get; set; }
    public string Unit { get; set; } = string.Empty;

    public string? Notes { get; set; }
}



