namespace CSM.Application.Procurement.EquipmentAssignments.Dtos;

public sealed record EquipmentAssignmentResponse(
    Guid Id,
    Guid CompanyId,
    string AssignmentNumber,
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    Guid ConstructionSiteId,
    string ConstructionSiteName,
    Guid? ProjectId,
    string? ProjectName,
    DateTime AssignedAtUtc,
    DateTime? ReleasedAtUtc,
    decimal? MeterReadingAtAssignment,
    decimal? MeterReadingAtRelease,
    string? Remarks,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
