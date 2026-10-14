namespace CSM.Application.Projects.Phases.Dtos;

public sealed record UpdateProjectPhaseRequest(
    string Name,
    string? Description,
    int Sequence,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate);