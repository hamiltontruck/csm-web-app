namespace CSM.Application.Sites.Dtos;

public sealed record CreateSiteRequest(
    Guid CompanyId,
    string SiteCode,
    string Name,
    string? Description,
    Guid? BusinessUnitId,
    string? Address,
    string? City,
    string? Region,
    string? Country,
    decimal? Latitude,
    decimal? Longitude,
    decimal? GeofenceRadiusMeters,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate);