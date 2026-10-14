namespace CSM.Application.Projects.Dtos;

public sealed record CreateProjectRequest(
    Guid ConstructionSiteId,
    string ProjectCode,
    string Name,
    string? Description,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    decimal ContractValue);