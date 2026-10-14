namespace CSM.Application.HRM.Timesheets.Dtos;

public sealed record GenerateTimesheetRequest(
    Guid EmployeeId,
    int Year,
    int Month);