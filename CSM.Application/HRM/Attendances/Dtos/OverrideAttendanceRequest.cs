using CSM.Domain.Enums;

namespace CSM.Application.HRM.Attendances.Dtos;

public sealed record OverrideAttendanceRequest(
    DateTime? CheckInAtUtc,
    DateTime? CheckOutAtUtc,
    AttendanceStatus Status,
    string Reason,
    string? Remarks);