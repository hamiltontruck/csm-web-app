using CSM.Domain.Enums;

namespace CSM.Application.Sites.Dtos;

public sealed record SiteResponse(
    Guid Id,
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
    DateOnly? PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    SiteStatus Status,
    bool IsActive,
    DateTime CreatedAtUtc);