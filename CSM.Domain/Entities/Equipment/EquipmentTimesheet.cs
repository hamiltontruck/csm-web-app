using CSM.Domain.Common;
using CSM.Domain.Entities.Procurement;

namespace CSM.Domain.Entities.Equipment;

public sealed class EquipmentTimesheet : TenantEntity
{
    public Guid EquipmentId { get; set; }

    public global::CSM.Domain.Entities.Procurement.Equipment Equipment { get; set; } = null!;

    public Guid SiteId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public string Shift { get; set; } = "Day";

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int BreakMinutes { get; set; }

    public decimal WorkedHours { get; set; }

    public string Activity { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}