using CSM.Domain.Common;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;

namespace CSM.Domain.Entities.Procurement;

public class DailyMaterialUsage : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid MaterialId { get; set; }

    public Guid DailyProgressLogId { get; set; }

    public Guid LoggedByUserId { get; set; }

    public DateOnly Date { get; set; }

    public decimal QuantityIssued { get; set; }

    public decimal QuantityUsed { get; set; }

    public decimal QuantityReturned { get; set; }
    public decimal QuantityVariance { get; set; }
    public bool RequiresSupervisorReview { get; set; }
    public string Unit { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public string UsageNumber { get; set; } = string.Empty;

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Material Material { get; set; } = null!;

    public DailyProgressLog DailyProgressLog { get; set; } = null!;

    public User LoggedByUser { get; set; } = null!;
}



