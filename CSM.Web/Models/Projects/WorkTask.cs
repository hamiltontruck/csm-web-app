namespace CSM.Web.Models.Projects;

public class WorkTask
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? ProjectPhaseId { get; set; }

    public Guid? MilestoneId { get; set; }

    public string TaskNumber { get; set; } = "";

    public string Title { get; set; } = "";

    public string? Description { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string Priority { get; set; } = "";

    public string Status { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
}