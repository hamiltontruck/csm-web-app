namespace CSM.Web.Models.Compliance;

public sealed class SafetyIncidentResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public Guid? EmployeeId { get; set; }

    public string IncidentNumber { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public Guid ReportedBy { get; set; }

    public string LocationDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public bool InjuryOccurred { get; set; }
    public bool MedicalTreatmentRequired { get; set; }
    public bool LostTimeIncident { get; set; }

    public string? ImmediateActionTaken { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }

    public Guid? InvestigatedBy { get; set; }

    public DateTime? InvestigationCompletedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}