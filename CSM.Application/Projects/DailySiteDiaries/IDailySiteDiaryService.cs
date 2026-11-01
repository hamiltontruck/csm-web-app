using CSM.Application.Projects.DailySiteDiaries.Dtos;

namespace CSM.Application.Projects.DailySiteDiaries;

public interface IDailySiteDiaryService
{
    Task<IReadOnlyCollection<DailySiteDiaryResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default);

    Task<DailySiteDiaryResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid diaryId,
        CancellationToken cancellationToken = default);

    Task<DailySiteDiaryResponse> CreateAsync(
        Guid currentUserId,
        CreateDailySiteDiaryRequest request,
        CancellationToken cancellationToken = default);

    Task<DailySiteDiaryResponse?> UpdateAsync(
        Guid currentUserId,
        Guid diaryId,
        UpdateDailySiteDiaryRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid currentUserId,
        Guid diaryId,
        CancellationToken cancellationToken = default);
}