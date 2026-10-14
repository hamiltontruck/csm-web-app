using System;

namespace CSM.Application.Procurement.EquipmentTimesheets;

public sealed record CreateEquipmentTimesheetRequest(
    Guid EquipmentId,
    Guid SiteId,
    DateTime WorkDate,
    string OperatorName,
    string Shift,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int BreakMinutes,
    string Activity,
    string? Remarks
);