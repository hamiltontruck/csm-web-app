using CSM.Domain.Enums;

namespace CSM.Application.Projects.ProjectDelays.Dtos;

public sealed record ProjectDelayResponse(
    Guid Id,
    Guid ProjectId,
    Guid SiteId,
    DateOnly DelayDate,
    DelayCategory Category,
    string Title,
    string Description,
    int TimeImpactDays,
    decimal CostImpact,
    string? MitigationAction,
    bool IsResolved);