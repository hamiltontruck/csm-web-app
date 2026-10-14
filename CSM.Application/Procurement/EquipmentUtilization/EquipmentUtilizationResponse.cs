using System;

namespace CSM.Application.Procurement.EquipmentUtilization;

public sealed record EquipmentUtilizationResponse(
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    Guid? ConstructionSiteId,
    string? ConstructionSiteName,
    decimal HoursWorked,
    decimal FuelConsumed,
    decimal DowntimeHours,
    decimal MaintenanceCost);