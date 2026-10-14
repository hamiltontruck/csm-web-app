using CSM.Domain.Enums;

namespace CSM.Application.Projects.ProjectDelays.Dtos;

public sealed class CreateProjectDelayRequest
{
    public Guid ProjectId { get; set; }

    public Guid SiteId { get; set; }

    public DateOnly DelayDate { get; set; }

    public DelayCategory Category { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int TimeImpactDays { get; set; }

    public decimal CostImpact { get; set; }

    public string? MitigationAction { get; set; }
}