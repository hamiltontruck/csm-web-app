namespace CSM.Application.Projects.DailyProgressTaskEntries.Dtos;

public sealed record DailyProgressTaskEntryResponse(
    Guid Id,
    Guid WorkTaskId,
    string TaskNumber,
    string Title,
    decimal ProgressPercentage,
    int LabourCount,
    int EquipmentCount,      // ← Add this
    decimal EquipmentHours,
    string? WorkCompleted,
    string? Remarks);