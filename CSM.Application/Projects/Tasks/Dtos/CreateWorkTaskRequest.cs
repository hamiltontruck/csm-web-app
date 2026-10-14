using CSM.Domain.Enums;

namespace CSM.Application.Projects.Tasks.Dtos;

public sealed record CreateWorkTaskRequest(
    Guid ProjectId,
    Guid? ProjectPhaseId,
    Guid? MilestoneId,
    string TaskNumber,
    string Title,
    string? Description,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Priority Priority);