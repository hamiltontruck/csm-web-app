namespace CSM.Application.HRM.Timesheets.Dtos;

public sealed record TimesheetEmployeeOptionResponse(
    Guid Id, string EmployeeNumber, string DisplayName);
