namespace CSM.Application.Projects.Milestones.Dtos;

public sealed record MilestoneResponse(
    Guid Id,
    string MilestoneNumber,
    Guid CompanyId,
    Guid ProjectId,
    Guid? ProjectPhaseId,
    string Name,
    string? Description,
    DateOnly PlannedDate,
    DateOnly? CompletedDate,
    bool IsCompleted,
    DateTime CreatedAtUtc);