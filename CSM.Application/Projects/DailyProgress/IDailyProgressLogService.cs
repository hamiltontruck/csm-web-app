using CSM.Application.Projects.DailyProgress.Dtos;

namespace CSM.Application.Projects.DailyProgress;

public interface IDailyProgressLogService
{
    Task<IReadOnlyCollection<DailyProgressLogResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);

    Task<DailyProgressLogResponse> GetByIdAsync(
        Guid currentUserId,
        Guid logId,
        CancellationToken cancellationToken = default);

    Task<DailyProgressLogResponse> CreateAsync(
        Guid currentUserId,
        CreateDailyProgressLogRequest request,
        CancellationToken cancellationToken = default);

    Task<DailyProgressLogResponse> UpdateAsync(
        Guid currentUserId,
        Guid logId,
        UpdateDailyProgressLogRequest request,
        CancellationToken cancellationToken = default);
}