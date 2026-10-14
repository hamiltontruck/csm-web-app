namespace CSM.Application.Projects.Progress.Dtos;

public sealed record ProjectProgressDashboardResponse(
    Guid ProjectId,
    string ProjectName,
    decimal PlannedProgress,
    decimal ActualProgress,
    int TotalTasks,
    int CompletedTasks,
    int RemainingTasks,
    int OpenDelays,
    int TotalDelayDays);