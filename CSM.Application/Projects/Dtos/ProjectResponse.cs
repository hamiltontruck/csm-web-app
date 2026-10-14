using CSM.Domain.Enums;

namespace CSM.Application.Projects.Dtos;

public sealed record ProjectResponse(
    Guid Id,

    Guid CompanyId,
    string CompanyName,

    Guid ConstructionSiteId,
    string ConstructionSiteName,

    string ProjectCode,
    string Name,
    string? Description,

    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,

    decimal ContractValue,
    decimal ProgressPercentage,

    ProjectStatus Status,
    DateTime CreatedAtUtc
);