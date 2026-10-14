using CSM.Domain.Enums;

namespace CSM.Application.HRM.Timesheets.Dtos;

public sealed record ReviewTimesheetRequest(
    TimesheetStatus Status,
    string? Remarks);
