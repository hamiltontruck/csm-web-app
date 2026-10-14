using CSM.Domain.Enums;

namespace CSM.Application.Features.Compliance.SafetyObservations.DTOs;

public sealed class CreateSafetyObservationRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string ObservationNumber { get; set; } = string.Empty;

    public ObservationCategory Category { get; set; }

    public DateTime ObservationDate { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public RiskLevel RiskLevel { get; set; }

    public bool PositiveObservation { get; set; }

    public string? ImmediateAction { get; set; }

    public string? RecommendedAction { get; set; }
}