using CSM.Domain.Common;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Projects;

public sealed class WbsTask : TenantEntity
{
    public Guid ProjectId { get; set; }

    public Guid? MilestoneId { get; set; }

    public Milestone? Milestone { get; set; }

    public Guid? AssignedEmployeeId { get; set; }

    public string TaskCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly PlannedStartDate { get; set; }

    public DateOnly PlannedFinishDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualFinishDate { get; set; }

    public WbsTaskStatus Status { get; set; } =
        WbsTaskStatus.NotStarted;

    public TaskPriority Priority { get; set; } =
        TaskPriority.Medium;

    public decimal ProgressPercentage { get; set; }

    public decimal EstimatedHours { get; set; }

    public decimal ActualHours { get; set; }

    public Project Project { get; set; } = null!;



    public Employee? AssignedEmployee { get; set; }

    public ICollection<TaskResourceAssignment> ResourceAssignments { get; set; }
        = new List<TaskResourceAssignment>();
}