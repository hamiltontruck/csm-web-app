namespace CSM.Application.HRM.Shifts.Dtos;

public sealed record ShiftResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes,
    decimal StandardHours,
    bool IsNightShift,
    bool IsActive,
    DateTime CreatedAtUtc);