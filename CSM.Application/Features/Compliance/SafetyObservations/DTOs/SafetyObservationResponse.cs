using CSM.Domain.Enums;

namespace CSM.Application.Features.Compliance.SafetyObservations.DTOs;

public sealed class SafetyObservationResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public Guid ObserverId { get; set; }
    public string ObserverName { get; set; } = string.Empty;

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}