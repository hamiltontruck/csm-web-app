namespace CSM.Web.Models.Security;

public sealed record UserSessionModel(
    Guid Id,
    string? DeviceName,
    string? CreatedByIp,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCurrent);