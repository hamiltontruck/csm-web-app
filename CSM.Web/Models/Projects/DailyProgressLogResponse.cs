namespace CSM.Web.Models.Projects;

public class DailyProgressLogResponse
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public DateTime LogDate { get; set; }

    public decimal ProgressPercentage { get; set; }

    public int LabourCount { get; set; }

    public int EquipmentCount { get; set; }

    public decimal EquipmentHours { get; set; }
}