using CSM.Domain.Enums;

namespace CSM.Application.HRM.Attendances.Dtos;

public sealed record CheckInRequest(
    Guid EmployeeId,
    Guid ConstructionSiteId,
    Guid? ShiftId,
    DateTime CheckInAtUtc,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    AttendanceSource Source,
    string? BiometricReference,
    string? Remarks);