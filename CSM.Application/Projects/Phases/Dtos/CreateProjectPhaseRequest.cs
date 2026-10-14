namespace CSM.Application.Projects.Phases.Dtos;

public sealed record CreateProjectPhaseRequest(
    Guid ProjectId,
    string PhaseNumber,
    string Name,
    string? Description,
    int Sequence,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate);