namespace CSM.Application.Features.Projects.DTOs;

public class CreateProjectDelayRequest
{
    public Guid ProjectId { get; set; }

    public DateTime DelayDate { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DelayDays { get; set; }

    public string? ActionTaken { get; set; }
}