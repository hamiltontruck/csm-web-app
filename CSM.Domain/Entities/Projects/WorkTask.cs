using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Projects;

public class WorkTask : TenantEntity
{
    public Guid ProjectId { get; set; }

    public Guid? ProjectPhaseId { get; set; }

    public Guid? MilestoneId { get; set; }

    public string TaskNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public Priority Priority { get; set; } = Priority.Normal;

    public WorkTaskStatus Status { get; set; }
        = WorkTaskStatus.NotStarted;

    public Project Project { get; set; } = null!;

    public ProjectPhase? ProjectPhase { get; set; }

    public Milestone? Milestone { get; set; }

    public ICollection<WorkTaskDependency> Dependencies { get; set; }
        = new List<WorkTaskDependency>();

    public ICollection<WorkTaskDependency> DependentTasks { get; set; }
        = new List<WorkTaskDependency>();

    public ICollection<DailyProgressTaskEntry> DailyProgressEntries { get; set; }
        = new List<DailyProgressTaskEntry>();
}