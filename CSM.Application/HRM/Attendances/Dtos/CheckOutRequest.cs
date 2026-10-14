namespace CSM.Application.HRM.Attendances.Dtos;

public sealed record CheckOutRequest(
    DateTime CheckOutAtUtc,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    string? Remarks);