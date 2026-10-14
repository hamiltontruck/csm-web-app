using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public class ProjectDelay : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateTime DelayDate { get; set; }

    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int DelayDays { get; set; }

    public bool IsResolved { get; set; }
    public DateTime? ResolvedDate { get; set; }

    public string? ActionTaken { get; set; }
}