using CSM.Domain.Enums;

namespace CSM.Application.Projects.Phases.Dtos;

public sealed record ProjectPhaseResponse(
    Guid Id,
    string PhaseNumber,
    Guid CompanyId,
    Guid ProjectId,
    string Name,
    string? Description,
    int Sequence,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    decimal ProgressPercentage,
    ProjectPhaseStatus Status,
    DateTime CreatedAtUtc);