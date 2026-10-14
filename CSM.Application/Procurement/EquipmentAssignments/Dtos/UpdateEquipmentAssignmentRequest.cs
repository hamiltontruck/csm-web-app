namespace CSM.Application.Procurement.EquipmentAssignments.Dtos;

public sealed record UpdateEquipmentAssignmentRequest(
    DateTime AssignedAtUtc,
    DateTime? ReleasedAtUtc,
    decimal? MeterReadingAtAssignment,
    decimal? MeterReadingAtRelease,
    string? Remarks);
