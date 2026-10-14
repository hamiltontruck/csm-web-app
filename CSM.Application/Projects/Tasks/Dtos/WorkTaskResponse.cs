using CSM.Domain.Enums;

namespace CSM.Application.Projects.Tasks.Dtos;

public sealed record WorkTaskResponse(
    Guid Id,
    Guid CompanyId,
    Guid ProjectId,
    Guid? ProjectPhaseId,
    Guid? MilestoneId,
    string TaskNumber,
    string Title,
    string? Description,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    decimal ProgressPercentage,
    Priority Priority,
    WorkTaskStatus Status,
    IReadOnlyCollection<WorkTaskDependencyResponse> Dependencies,
    DateTime CreatedAtUtc);