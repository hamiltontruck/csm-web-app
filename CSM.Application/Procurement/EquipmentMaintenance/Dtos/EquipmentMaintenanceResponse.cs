namespace CSM.Application.Procurement.EquipmentMaintenance.Dtos;

public sealed record EquipmentMaintenanceResponse(
    Guid Id,
    Guid CompanyId,
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    string MaintenanceType,
    DateTime ScheduledAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    decimal? MeterReading,
    decimal? Cost,
    string CurrencyCode,
    string? ServiceProvider,
    string? Description,
    string? PartsReplaced,
    DateTime? NextMaintenanceAtUtc,
    decimal? NextMaintenanceMeterReading,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
