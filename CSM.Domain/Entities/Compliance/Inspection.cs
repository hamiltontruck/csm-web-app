using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Compliance;

public class Inspection : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string InspectionNumber { get; set; } = string.Empty;

    public string InspectionType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTime ScheduledAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public Guid? InspectorEmployeeId { get; set; }

    public string? ExternalInspectorName { get; set; }

    public string? ExternalOrganization { get; set; }

    public InspectionStatus Status { get; set; }
        = InspectionStatus.Scheduled;

    public string? Summary { get; set; }

    public string? CorrectiveActionRequired { get; set; }

    public DateOnly? CorrectiveActionDueDate { get; set; }

    public Guid? SignedOffBy { get; set; }

    public DateTime? SignedOffAtUtc { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }

    public ICollection<InspectionChecklistItem> ChecklistItems { get; set; }
        = new List<InspectionChecklistItem>();
}