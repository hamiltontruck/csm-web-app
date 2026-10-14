using CSM.Domain.Enums;

namespace CSM.Application.Projects.Tasks.Dtos;

public sealed record UpdateWorkTaskRequest(
    Guid? ProjectPhaseId,
    Guid? MilestoneId,
    string Title,
    string? Description,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Priority Priority);