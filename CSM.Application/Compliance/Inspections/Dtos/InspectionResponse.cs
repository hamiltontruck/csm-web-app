using CSM.Domain.Enums;

namespace CSM.Application.Compliance.Inspections.Dtos;

public sealed class InspectionResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string InspectionNumber { get; set; } = string.Empty;

    public string InspectionType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ConstructionSiteName { get; set; } = string.Empty;

    public DateTime ScheduledAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public Guid? InspectorEmployeeId { get; set; }

    public string? ExternalInspectorName { get; set; }

    public string? ExternalOrganization { get; set; }

    public InspectionStatus Status { get; set; }

    public string? Summary { get; set; }

    public string? CorrectiveActionRequired { get; set; }

    public DateOnly? CorrectiveActionDueDate { get; set; }

    public Guid? SignedOffBy { get; set; }

    public DateTime? SignedOffAtUtc { get; set; }

    public IReadOnlyCollection<InspectionChecklistItemResponse>
        ChecklistItems { get; set; }
        = Array.Empty<InspectionChecklistItemResponse>();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
