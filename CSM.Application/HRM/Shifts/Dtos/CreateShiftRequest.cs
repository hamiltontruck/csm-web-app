namespace CSM.Application.HRM.Shifts.Dtos;

public sealed record CreateShiftRequest(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes,
    decimal StandardHours,
    bool IsNightShift);