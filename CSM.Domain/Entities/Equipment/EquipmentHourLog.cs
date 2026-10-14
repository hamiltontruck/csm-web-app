using CSM.Domain.Common;

namespace CSM.Domain.Entities.Equipment;

public class EquipmentHourLog : TenantEntity
{
    public Guid EquipmentId { get; set; }

    public global::CSM.Domain.Entities.Procurement.Equipment Equipment { get; set; } = null!;

    public DateTime LoggedAtUtc { get; set; }

    public decimal PreviousMeterReading { get; set; }

    public decimal CurrentMeterReading { get; set; }

    public decimal HoursWorked { get; set; }

    public string? OperatorEmployeeId { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public string Activity { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}