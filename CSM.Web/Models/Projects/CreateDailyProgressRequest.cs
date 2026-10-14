namespace CSM.Web.Models.Projects;

public class CreateDailyProgressRequest
{
    public Guid ProjectId { get; set; }

    public Guid TaskId { get; set; }

    public DateOnly LogDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public int LabourCount { get; set; }

    public decimal EquipmentHours { get; set; }

    public string WorkDescription { get; set; } = "";

    public string? Remarks { get; set; }
}