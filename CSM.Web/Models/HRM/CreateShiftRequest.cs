namespace CSM.Web.Models.HRM;

public sealed class CreateShiftRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int GracePeriodMinutes { get; set; }
    public decimal StandardHours { get; set; }

    public bool IsNightShift { get; set; }
}