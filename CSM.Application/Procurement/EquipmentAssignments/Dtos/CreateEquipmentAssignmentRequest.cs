namespace CSM.Application.Procurement.EquipmentAssignments.Dtos;

public sealed record CreateEquipmentAssignmentRequest(
    Guid EquipmentId,
    Guid ConstructionSiteId,
    Guid? ProjectId,
    DateTime AssignedAtUtc,
    decimal? MeterReadingAtAssignment,
    string? Remarks);
