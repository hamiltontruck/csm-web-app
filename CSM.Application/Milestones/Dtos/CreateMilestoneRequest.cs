namespace CSM.Application.Projects.Milestones.Dtos;

public sealed record CreateMilestoneRequest(
    Guid ProjectId,
    Guid? ProjectPhaseId,
    string Name,
    string? Description,
    DateOnly PlannedDate);