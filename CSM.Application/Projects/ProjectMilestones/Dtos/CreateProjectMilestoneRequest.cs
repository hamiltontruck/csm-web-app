using CSM.Domain.Enums;

namespace CSM.Application.Projects.ProjectMilestones.Dtos;

public sealed class CreateProjectMilestoneRequest
{
    public Guid ProjectId { get; set; }

    public Guid ProjectPhaseId { get; set; }

    public string MilestoneNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly PlannedDate { get; set; }

    public DateOnly? ActualDate { get; set; }

    public MilestoneStatus Status { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string? Notes { get; set; }
}