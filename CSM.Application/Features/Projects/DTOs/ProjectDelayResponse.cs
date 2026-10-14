namespace CSM.Application.Features.Projects.DTOs;

public class ProjectDelayResponse
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public DateTime DelayDate { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DelayDays { get; set; }

    public bool IsResolved { get; set; }

    public DateTime? ResolvedDate { get; set; }

    public string? ActionTaken { get; set; }
}