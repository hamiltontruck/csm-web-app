using CSM.Domain.Enums;

namespace CSM.Application.Compliance.SafetyIncidents.Dtos;

public sealed class UpdateSafetyIncidentRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string IncidentNumber { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime ReportedAtUtc { get; set; }

    public string LocationDescription { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public SafetyIncidentSeverity Severity { get; set; }

    public SafetyIncidentStatus Status { get; set; }

    public bool InjuryOccurred { get; set; }

    public bool MedicalTreatmentRequired { get; set; }

    public bool LostTimeIncident { get; set; }

    public string? ImmediateActionTaken { get; set; }

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }

    public Guid? InvestigatedBy { get; set; }

    public DateTime? InvestigationCompletedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
}
