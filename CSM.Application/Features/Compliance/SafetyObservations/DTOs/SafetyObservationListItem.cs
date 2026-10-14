using CSM.Domain.Enums;

namespace CSM.Application.Features.Compliance.SafetyObservations.DTOs;

public sealed class SafetyObservationListItem
{
    public Guid Id { get; set; }

    public string ObservationNumber { get; set; } = string.Empty;

    public string ConstructionSiteName { get; set; } = string.Empty;

    public DateTime ObservationDate { get; set; }

    public ObservationCategory Category { get; set; }

    public RiskLevel RiskLevel { get; set; }

    public ObservationStatus Status { get; set; }
}