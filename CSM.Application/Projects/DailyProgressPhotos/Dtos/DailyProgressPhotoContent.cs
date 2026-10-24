namespace CSM.Application.Projects.DailyProgressPhotos.Dtos;

public sealed record DailyProgressPhotoContent(
    Stream Content,
    string ContentType,
    string FileName);
