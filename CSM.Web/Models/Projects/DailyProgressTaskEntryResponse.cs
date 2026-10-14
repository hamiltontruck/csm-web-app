namespace CSM.Web.Models.Projects;

public class DailyProgressTaskEntryResponse
{
    public Guid Id { get; set; }

    public Guid WorkTaskId { get; set; }

    public string TaskNumber { get; set; } = "";

    public string TaskTitle { get; set; } = "";

    public decimal ProgressPercentage { get; set; }

    public int LabourCount { get; set; }

    public decimal EquipmentHours { get; set; }

    public string WorkCompleted { get; set; } = "";

    public string? Remarks { get; set; }

    public int EquipmentCount { get; set; }
}