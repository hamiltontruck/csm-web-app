namespace CSM.Application.Procurement.EquipmentDowntime.Dtos;

public sealed record EquipmentDowntimeResponse(
    Guid Id,
    Guid CompanyId,
    Guid EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    Guid? ConstructionSiteId,
    string? ConstructionSiteCode,
    string? ConstructionSiteName,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string DowntimeNumber,
    string Reason,
    string? Resolution,
    double? DurationHours,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
