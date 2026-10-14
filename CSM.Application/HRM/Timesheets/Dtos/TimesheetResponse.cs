using CSM.Domain.Enums;

namespace CSM.Application.HRM.Timesheets.Dtos;

public sealed record TimesheetResponse(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal TotalHours,
    TimesheetStatus Status,
    Guid? SubmittedBy,
    DateTime? SubmittedAtUtc,
    Guid? ApprovedBy,
    DateTime? ApprovedAtUtc,
    string? ApprovalRemarks,
    string? Remarks,
    DateTime CreatedAtUtc);
