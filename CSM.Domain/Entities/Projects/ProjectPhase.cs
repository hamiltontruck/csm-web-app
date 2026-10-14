using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Projects;

public class ProjectPhase : TenantEntity
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string PhaseNumber { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public ProjectPhaseStatus Status { get; set; }
        = ProjectPhaseStatus.NotStarted;

    public Project Project { get; set; } = null!;

    public ICollection<Milestone> Milestones { get; set; }
        = new List<Milestone>();

    public ICollection<WorkTask> Tasks { get; set; }
        = new List<WorkTask>();


}