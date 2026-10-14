using CSM.Domain.Common;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Compliance;

public class SafetyObservation : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }
    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid ObserverId { get; set; }
    public User Observer { get; set; } = null!;

    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string ObservationNumber { get; set; } = string.Empty;

    public ObservationCategory Category { get; set; }

    public DateTime ObservationDate { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public RiskLevel RiskLevel { get; set; }

    public bool PositiveObservation { get; set; }

    public string? ImmediateAction { get; set; }

    public string? RecommendedAction { get; set; }

    public ObservationStatus Status { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
}