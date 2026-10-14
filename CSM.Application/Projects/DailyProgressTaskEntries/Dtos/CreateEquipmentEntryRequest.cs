namespace CSM.Application.Projects.DailyProgressTaskEntries.Dtos;

public sealed record CreateEquipmentEntryRequest(
    Guid DailyProgressTaskEntryId,
    Guid EquipmentId,
    string EquipmentName,
    decimal HoursUsed,
    string? OperatorName,
    string? Remarks);