using System;

namespace CSM.Application.Procurement.EquipmentHourLogs;

public sealed record CreateEquipmentHourLogRequest(
    Guid EquipmentId,
    Guid SiteId,
    DateTime LogDateUtc,
    decimal PreviousMeter,
    decimal CurrentMeter,
    string OperatorName,
    string Activity,
    string? Remarks
);