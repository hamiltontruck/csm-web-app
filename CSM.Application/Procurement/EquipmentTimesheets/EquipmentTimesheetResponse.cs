using System;

namespace CSM.Application.Procurement.EquipmentTimesheets;

public sealed record EquipmentTimesheetResponse(
    Guid Id,
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    Guid SiteId,
    string SiteName,
    DateTime WorkDate,
    string OperatorName,
    string Shift,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int BreakMinutes,
    decimal WorkedHours,
    string Activity,
    string? Remarks
);