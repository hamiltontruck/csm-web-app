namespace CSM.Web.Models.Equipment;

public sealed class CreateEquipmentHourLogRequest
{
    public Guid EquipmentId { get; set; }

    public Guid SiteId { get; set; }

    public DateTime LogDateUtc { get; set; }

    public decimal PreviousMeter { get; set; }

    public decimal CurrentMeter { get; set; }

    public string OperatorName { get; set; } = string.Empty;

    public string Activity { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}