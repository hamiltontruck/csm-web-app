namespace CSM.Application.Users.Dtos;

public sealed record CreateUserRequest(
    Guid? CompanyId,
    Guid? EmployeeId,
    string Email,
    string FirstName,
    string LastName,
    string TemporaryPassword,
    string PhoneNumber,
    string JobTitle,
    Guid? DefaultSiteId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Guid> SiteIds
);