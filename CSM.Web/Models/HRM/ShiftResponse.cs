namespace CSM.Web.Models.HRM;

public sealed class ShiftResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int GracePeriodMinutes { get; set; }
    public decimal StandardHours { get; set; }

    public bool IsNightShift { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}