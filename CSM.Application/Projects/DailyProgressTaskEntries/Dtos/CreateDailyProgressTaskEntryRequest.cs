
using CSM.Domain.Enums;

namespace CSM.Application.Projects.DailyProgressTaskEntries.Dtos;

public sealed record CreateDailyProgressTaskEntryRequest(
    Guid DailyProgressLogId,
    Guid WorkTaskId,
    decimal ProgressPercentage,
    int LabourCount,
    decimal EquipmentHours,
    string WorkCompleted,
    string? Remarks);
