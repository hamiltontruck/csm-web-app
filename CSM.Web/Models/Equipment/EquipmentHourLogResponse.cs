namespace CSM.Web.Models.Equipment;

public sealed class EquipmentHourLogResponse
{
    public Guid Id { get; set; }

    public Guid EquipmentId { get; set; }

    public string EquipmentCode { get; set; } = string.Empty;

    public string EquipmentName { get; set; } = string.Empty;

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