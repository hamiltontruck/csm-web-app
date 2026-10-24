namespace CSM.Application.Auth.Dtos;

public sealed record UserSessionResponse(
    Guid Id,
    string? DeviceName,
    string? CreatedByIp,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCurrent);
