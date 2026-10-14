namespace CSM.Application.Procurement.EquipmentMaintenance.Dtos;

public sealed record CreateEquipmentMaintenanceRequest(
    Guid EquipmentId,
    string MaintenanceType,
    DateTime ScheduledAtUtc,
    decimal? MeterReading,
    decimal? Cost,
    string CurrencyCode,
    string? ServiceProvider,
    string? Description,
    string? PartsReplaced,
    DateTime? NextMaintenanceAtUtc,
    decimal? NextMaintenanceMeterReading);
