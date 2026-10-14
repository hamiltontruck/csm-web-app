namespace CSM.Application.Users.Dtos;

public sealed record UserProfileResponse(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string JobTitle,
    bool IsActive,
    Guid? DefaultSiteId,
    string? DefaultSiteName,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<SiteSummaryDto> Sites
);

public sealed record SiteSummaryDto(
    Guid Id,
    string Name,
    bool IsDefault
);