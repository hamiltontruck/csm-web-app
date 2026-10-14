using CSM.Domain.Enums;

namespace CSM.Application.Projects.WbsTasks.Dtos;

public sealed class CreateWbsTaskRequest
{
    public Guid ProjectId { get; set; }

    public Guid MilestoneId { get; set; }

    public Guid? AssignedEmployeeId { get; set; }

    public string TaskCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly PlannedStartDate { get; set; }

    public DateOnly PlannedFinishDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualFinishDate { get; set; }

    public WbsTaskStatus Status { get; set; }

    public TaskPriority Priority { get; set; }

    public decimal ProgressPercentage { get; set; }

    public decimal EstimatedHours { get; set; }

    public decimal ActualHours { get; set; }
}