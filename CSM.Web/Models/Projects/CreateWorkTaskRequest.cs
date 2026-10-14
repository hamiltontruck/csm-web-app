namespace CSM.Web.Models.Projects;

public class CreateWorkTaskRequest
{
    public Guid ProjectId { get; set; }

    public Guid? ProjectPhaseId { get; set; }

    public Guid? MilestoneId { get; set; }

    public string TaskNumber { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;
}