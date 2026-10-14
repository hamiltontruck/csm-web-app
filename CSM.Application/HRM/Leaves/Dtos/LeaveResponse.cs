using CSM.Domain.Enums;

namespace CSM.Application.HRM.Leaves.Dtos;

public sealed record LeaveResponse(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NumberOfDays,
    string? Reason,
    LeaveStatus Status,
    Guid? ReviewedBy,
    DateTime? ReviewedAtUtc,
    string? ReviewRemarks,
    DateTime CreatedAtUtc);
