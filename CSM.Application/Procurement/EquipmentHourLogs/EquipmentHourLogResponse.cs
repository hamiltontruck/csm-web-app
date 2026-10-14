using System;

namespace CSM.Application.Procurement.EquipmentHourLogs;

public sealed record EquipmentHourLogResponse(
    Guid Id,
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,

    DateTime LoggedAtUtc,

    decimal PreviousMeterReading,
    decimal CurrentMeterReading,
    decimal HoursWorked,

    string? OperatorEmployeeId,
    string OperatorName,

    Guid? ProjectId,
    string? ProjectName,

    string Activity,
    string? Remarks
);