using CSM.Domain.Enums;

namespace CSM.Application.HRM.Leaves.Dtos;

public sealed record ReviewLeaveRequest(
    LeaveStatus Status,
    string? Remarks);
