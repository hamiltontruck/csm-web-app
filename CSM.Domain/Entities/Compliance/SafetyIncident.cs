using CSM.Domain.Common;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Compliance;

public class SafetyIncident : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string IncidentNumber { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid ReportedBy { get; set; }

    public string LocationDescription { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public SafetyIncidentSeverity Severity { get; set; }

    public SafetyIncidentStatus Status { get; set; }
        = SafetyIncidentStatus.Reported;

    public bool InjuryOccurred { get; set; }

    public bool MedicalTreatmentRequired { get; set; }

    public bool LostTimeIncident { get; set; }

    public string? ImmediateActionTaken { get; set; }

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }

    public Guid? InvestigatedBy { get; set; }

    public DateTime? InvestigationCompletedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }

    public Employee? Employee { get; set; }
}