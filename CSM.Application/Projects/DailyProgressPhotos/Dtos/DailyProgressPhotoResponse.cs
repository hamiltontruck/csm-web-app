namespace CSM.Application.Projects.DailyProgressPhotos.Dtos;

public sealed record DailyProgressPhotoResponse(
    Guid Id,
    Guid DailySiteDiaryId,
    string FileName,
    string FilePath,
    string? Caption,
    DateTime UploadedAtUtc);