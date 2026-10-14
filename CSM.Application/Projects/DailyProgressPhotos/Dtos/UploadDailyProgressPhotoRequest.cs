namespace CSM.Application.Projects.DailyProgressPhotos.Dtos;

public sealed class UploadDailyProgressPhotoRequest
{
    public Guid DailySiteDiaryId { get; set; }

    public string? Caption { get; set; }
}