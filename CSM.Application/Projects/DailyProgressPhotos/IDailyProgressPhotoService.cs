using CSM.Application.Projects.DailyProgressPhotos.Dtos;

namespace CSM.Application.Projects.DailyProgressPhotos;

public interface IDailyProgressPhotoService
{
    Task<IReadOnlyCollection<DailyProgressPhotoResponse>> GetByDiaryAsync(
        Guid companyId,
        Guid diaryId,
        CancellationToken cancellationToken = default);

    Task<DailyProgressPhotoResponse> UploadAsync(
        Guid companyId,
        Guid diaryId,
        Stream fileStream,
        string fileName,
        string? caption,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid companyId,
        Guid photoId,
        CancellationToken cancellationToken = default);
}