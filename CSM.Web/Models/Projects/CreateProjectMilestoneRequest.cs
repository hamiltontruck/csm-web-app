namespace CSM.Web.Models.Projects;

public class CreateProjectMilestoneRequest
{
    public Guid ProjectId { get; set; }

    public string MilestoneNumber { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public DateOnly PlannedDate { get; set; }

    public string? Notes { get; set; }
}