namespace CSM.Application.Users.Dtos;

public sealed record UpdateUserRequest(
    string FirstName,
    string LastName,
    Guid? EmployeeId,
    string PhoneNumber,
    string JobTitle,
    Guid? DefaultSiteId,
    bool IsActive,
    IReadOnlyCollection<Guid> SiteIds
);