namespace CSM.Application.Projects.DailyProgressTaskEntries.Dtos;

public sealed record EquipmentEntryResponse(
    Guid Id,
    Guid EquipmentId,
    string EquipmentName,
    decimal HoursUsed,
    string? OperatorName,
    string? Remarks);