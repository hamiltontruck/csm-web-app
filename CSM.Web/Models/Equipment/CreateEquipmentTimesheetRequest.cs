namespace CSM.Web.Models.Equipment;

public sealed class CreateEquipmentTimesheetRequest
{
    public Guid EquipmentId { get; set; }

    public Guid SiteId { get; set; }

    public DateTime WorkDate { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public string Shift { get; set; } = "Day";

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public int BreakMinutes { get; set; }

    public string Activity { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}