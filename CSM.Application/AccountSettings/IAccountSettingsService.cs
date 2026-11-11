using CSM.Application.Auth.Dtos;
using CSM.Application.Users.Dtos;

namespace CSM.Application.AccountSettings;

public sealed record OwnProfileResponse(string FirstName, string LastName, string PhoneNumber, string JobTitle)
{
    public Guid UserId { get; init; }
    public Guid? CompanyId { get; init; }
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public bool HasActiveSiteAccess { get; init; }
}
public sealed record MembershipResponse(Guid CompanyId, Guid SiteId, string SiteCode, string SiteName,
    string? Address, string? City, string? Region, string? Country, string SiteStatus, bool IsDefault);
public sealed record LoginHistoryResponse(Guid Id, DateTime OccurredAtUtc, bool Succeeded,
    string? IpAddress, string? DeviceName, string? FailureReason);

public interface IAccountSettingsService
{
    Task<OwnProfileResponse> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<OwnProfileResponse> UpdateProfileAsync(Guid userId, UpdateMyProfileRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MembershipResponse>> GetMembershipsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LoginHistoryResponse>> GetLoginHistoryAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserSessionResponse>> GetSessionsAsync(Guid userId, string? currentRefreshToken, CancellationToken ct = default);
    Task RevokeSessionAsync(Guid userId, Guid sessionId, string? currentRefreshToken, string? ip, CancellationToken ct = default);
    Task<int> RevokeOtherSessionsAsync(Guid userId, string? currentRefreshToken, string? ip, CancellationToken ct = default);
}
