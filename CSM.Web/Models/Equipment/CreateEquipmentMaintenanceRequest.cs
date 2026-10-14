namespace CSM.Web.Models.Equipment;

public sealed class CreateEquipmentMaintenanceRequest
{
    public Guid EquipmentId { get; set; }

    public string MaintenanceType { get; set; } = string.Empty;

    public DateTime ScheduledAtUtc { get; set; }

    public decimal? MeterReading { get; set; }
    public decimal? Cost { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public string? ServiceProvider { get; set; }
    public string? Description { get; set; }
    public string? PartsReplaced { get; set; }

    public DateTime? NextMaintenanceAtUtc { get; set; }
    public decimal? NextMaintenanceMeterReading { get; set; }
}