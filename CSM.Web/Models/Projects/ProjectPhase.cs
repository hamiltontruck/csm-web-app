namespace CSM.Web.Models.Projects;

public class ProjectPhase
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string PhaseNumber { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public int Sequence { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string Status { get; set; } = "";
}