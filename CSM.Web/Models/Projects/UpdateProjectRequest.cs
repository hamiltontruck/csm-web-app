namespace CSM.Web.Models.Projects;

public class UpdateProjectRequest
{
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string Status { get; set; } = "Planning";

    public decimal ProgressPercentage { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }
}