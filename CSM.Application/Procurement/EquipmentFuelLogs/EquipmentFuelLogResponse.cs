using System;

namespace CSM.Application.Procurement.EquipmentFuelLogs;

public sealed record EquipmentFuelLogResponse(
    Guid Id,
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    Guid? SiteId,
    string? SiteName,
    DateTime RefueledAtUtc,
    decimal Liters,
    decimal UnitPrice,
    decimal TotalCost,
    decimal MeterReading,
    string FuelType,
    string? SupplierName,
    string? InvoiceNumber,
    string? Remarks);