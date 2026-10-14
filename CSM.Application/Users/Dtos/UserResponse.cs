namespace CSM.Application.Users.Dtos;

public sealed record UserResponse(
    Guid Id,
    Guid? CompanyId,
    Guid? EmployeeId,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string PhoneNumber,
    string JobTitle,
    bool IsActive,
    bool MustChangePassword,
    Guid? DefaultSiteId,
    DateTime? LastLoginAtUtc,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Guid> SiteAccesses,
    IReadOnlyCollection<string> SiteNames,
    DateTime CreatedAtUtc
);