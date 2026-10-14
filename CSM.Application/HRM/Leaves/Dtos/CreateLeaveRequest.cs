namespace CSM.Application.HRM.Leaves.Dtos;

public sealed record CreateLeaveRequest(
    Guid EmployeeId,
    string LeaveType,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason);
