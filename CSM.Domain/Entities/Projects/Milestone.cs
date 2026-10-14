using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Projects;

public class Milestone : TenantEntity
{
    public Guid ProjectId { get; set; }

    public Guid? ProjectPhaseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string MilestoneNumber { get; set; } = string.Empty;

    public DateOnly PlannedDate { get; set; }

    public DateOnly? ActualDate { get; set; }

    public MilestoneStatus Status { get; set; }
        = MilestoneStatus.Pending;

    public decimal ProgressPercentage { get; set; }

    public string? Notes { get; set; }

    public Project Project { get; set; } = null!;

    public ProjectPhase? ProjectPhase { get; set; }
}