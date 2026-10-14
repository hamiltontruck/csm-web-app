namespace CSM.Application.Projects.DailyProgress.Dtos;

public sealed record DailyProgressLogResponse(
    Guid Id,
    string LogNumber,
    Guid CompanyId,
    Guid ProjectId,
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
    string? Remarks,
    DateTime CreatedAtUtc);