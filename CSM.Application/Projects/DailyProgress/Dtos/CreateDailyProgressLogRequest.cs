namespace CSM.Application.Projects.DailyProgress.Dtos;

public sealed record CreateDailyProgressLogRequest(
    Guid ProjectId,
    Guid TaskId,
    DateOnly LogDate,
    string? WeatherCondition,
    decimal? TemperatureCelsius,
    int LaborCount,
    int ContractorLaborCount,
    decimal ProgressPercentage,
    string? WorkCompleted,
    string? WorkPlannedNext,
    string? MaterialsUsedSummary,
    string? EquipmentUsedSummary,
    string? Delays,
    string? Issues,
    string? SafetyNotes,
    string? Remarks);