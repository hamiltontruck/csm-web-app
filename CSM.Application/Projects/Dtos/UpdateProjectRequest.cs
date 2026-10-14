namespace CSM.Application.Projects.Dtos;

public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    string Status,
    decimal ProgressPercentage,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    decimal ContractValue);