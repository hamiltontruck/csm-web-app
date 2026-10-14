namespace CSM.Web.Models.Procurement;

public class DailyMaterialUsageResponse
{
    public Guid Id { get; set; }

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = "";

    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";

    public Guid DailyProgressLogId { get; set; }

    public Guid LoggedByUserId { get; set; }
    public string LoggedByUserName { get; set; } = "";

    public DateOnly Date { get; set; }

    public decimal QuantityIssued { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal QuantityVariance { get; set; }

    public bool RequiresSupervisorReview { get; set; }

    public string Unit { get; set; } = "";
    public string? Notes { get; set; }
}