namespace CSM.Application.HRM.Shifts.Dtos;

public sealed record UpdateShiftRequest(
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int GracePeriodMinutes,
    decimal StandardHours,
    bool IsNightShift);