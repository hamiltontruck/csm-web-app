namespace CSM.Web.Models.Compliance;

public sealed class CreateSafetyIncidentRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string IncidentNumber { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public string LocationDescription { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Severity { get; set; }

    public int Status { get; set; } = 1;

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