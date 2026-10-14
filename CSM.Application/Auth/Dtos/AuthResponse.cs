namespace CSM.Application.Auth.Dtos;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    Guid UserId,
    Guid? CompanyId,
    Guid? EmployeeId,
    string Email,
    string FullName,
    bool MustChangePassword,
    IReadOnlyCollection<string> Roles);