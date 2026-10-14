namespace CSM.Application.Procurement.EquipmentDowntime.Dtos;

public sealed record CreateEquipmentDowntimeRequest(
    Guid EquipmentId,
    Guid? ConstructionSiteId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string DowntimeNumber,
    string Reason,
    string? Resolution);
