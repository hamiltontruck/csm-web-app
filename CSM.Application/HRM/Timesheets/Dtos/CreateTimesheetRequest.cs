namespace CSM.Application.HRM.Timesheets.Dtos;

public sealed record CreateTimesheetRequest(
    Guid EmployeeId,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    string? Remarks);
