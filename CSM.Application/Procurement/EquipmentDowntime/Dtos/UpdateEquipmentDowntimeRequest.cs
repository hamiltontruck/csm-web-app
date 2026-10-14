namespace CSM.Application.Procurement.EquipmentDowntime.Dtos;

public sealed record UpdateEquipmentDowntimeRequest(
    Guid? ConstructionSiteId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string Reason,
    string? Resolution);
