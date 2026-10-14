namespace CSM.Web.Models.Projects;

public class UpdateProjectPhaseRequest
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public int Sequence { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string Status { get; set; } = "Planning";
}