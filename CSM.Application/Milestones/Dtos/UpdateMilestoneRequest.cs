namespace CSM.Application.Projects.Milestones.Dtos;

public sealed record UpdateMilestoneRequest(
    Guid? ProjectPhaseId,
    string Name,
    string? Description,
    DateOnly PlannedDate);