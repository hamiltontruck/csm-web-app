using CSM.Domain.Common;

namespace CSM.Domain.Entities.HRM;

public class Shift : TenantEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int GracePeriodMinutes { get; set; }

    public decimal StandardHours { get; set; } = 8;

    public bool IsNightShift { get; set; }

    public bool IsActive { get; set; } = true;
}