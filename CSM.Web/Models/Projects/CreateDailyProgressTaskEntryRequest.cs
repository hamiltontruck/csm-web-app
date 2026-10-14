namespace CSM.Web.Models.Projects;

public class CreateDailyProgressTaskEntryRequest
{
    public Guid DailyProgressLogId { get; set; }

    public Guid WorkTaskId { get; set; }

    public decimal ProgressPercentage { get; set; }

    public int LabourCount { get; set; }

    public decimal EquipmentHours { get; set; }

    public string WorkCompleted { get; set; } = "";

    public string? Remarks { get; set; }
}