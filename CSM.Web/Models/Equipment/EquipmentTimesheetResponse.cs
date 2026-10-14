namespace CSM.Web.Models.Equipment;

public sealed class EquipmentTimesheetResponse
{
    public Guid Id { get; set; }

    public Guid EquipmentId { get; set; }

    public string EquipmentCode { get; set; } = string.Empty;

    public string EquipmentName { get; set; } = string.Empty;

    public Guid SiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public DateTime WorkDate { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public string Shift { get; set; } = "Day";

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public int BreakMinutes { get; set; }

    public decimal WorkedHours { get; set; }

    public string Activity { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}