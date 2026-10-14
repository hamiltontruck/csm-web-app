namespace CSM.Web.Models.Equipment;

public sealed class EquipmentMaintenanceResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EquipmentId { get; set; }

    public string EquipmentCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;

    public string MaintenanceType { get; set; } = string.Empty;

    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public decimal? MeterReading { get; set; }
    public decimal? Cost { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public string? ServiceProvider { get; set; }
    public string? Description { get; set; }
    public string? PartsReplaced { get; set; }

    public DateTime? NextMaintenanceAtUtc { get; set; }
    public decimal? NextMaintenanceMeterReading { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}