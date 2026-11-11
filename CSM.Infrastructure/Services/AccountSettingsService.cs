using CSM.Application.AccountSettings;
using CSM.Application.Auth.Dtos;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Users.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class AccountSettingsService(ApplicationDbContext db, ITokenService tokens) : IAccountSettingsService
{
    private async Task<User> Actor(Guid id, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsDeleted, ct)
        ?? throw new AuthenticationException("The authenticated account is unavailable.");

    private static OwnProfileResponse Profile(User user) => new(user.FirstName, user.LastName, user.PhoneNumber, user.JobTitle);

    public async Task<OwnProfileResponse> GetProfileAsync(Guid userId, CancellationToken ct = default) => Profile(await Actor(userId, ct));

    public async Task<OwnProfileResponse> UpdateProfileAsync(Guid userId, UpdateMyProfileRequest request, CancellationToken ct = default)
    {
        var actor = await Actor(userId, ct);
        ArgumentNullException.ThrowIfNull(request);
        var first = Text(request.FirstName, "First name", 100, true);
        var last = Text(request.LastName, "Last name", 100, true);
        var phone = Text(request.PhoneNumber, "Phone number", 30);
        var job = Text(request.JobTitle, "Job title", 100);
        actor.FirstName = first; actor.LastName = last; actor.PhoneNumber = phone; actor.JobTitle = job;
        actor.UpdatedBy = actor.Id; actor.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Profile(actor);
    }

    private static string Text(string? value, string label, int max, bool required = false)
    {
        value = value?.Trim() ?? "";
        if ((required && value.Length == 0) || value.Length > max)
            throw new UserManagementException($"{label} {(required ? "is required and " : "")}cannot exceed {max} characters.");
        return value;
    }

    public async Task<IReadOnlyList<MembershipResponse>> GetMembershipsAsync(Guid userId, CancellationToken ct = default)
    {
        var actor = await Actor(userId, ct);
        if (actor.CompanyId is not { } company || company == Guid.Empty) return [];
        return await db.UserSiteAccesses.AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == company && x.IsActive && !x.IsDeleted &&
                x.Site.CompanyId == company && x.Site.IsActive && !x.Site.IsDeleted)
            .OrderBy(x => x.Site.Name)
            .Select(x => new MembershipResponse(company, x.SiteId, x.Site.SiteCode, x.Site.Name,
                x.Site.Address, x.Site.City, x.Site.Region, x.Site.Country, x.Site.Status.ToString(), x.SiteId == actor.DefaultSiteId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LoginHistoryResponse>> GetLoginHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        await Actor(userId, ct);
        return await db.Set<LoginHistoryEntry>().AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).Take(100)
            .Select(x => new LoginHistoryResponse(x.Id, x.OccurredAtUtc, x.Succeeded, x.IpAddress, x.DeviceName, x.FailureReason)).ToListAsync(ct);
    }

    private async Task<RefreshToken> Current(Guid userId, string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new AuthenticationException("The current session could not be verified. Sign in again.");
        var hash = tokens.HashRefreshToken(raw); var now = DateTime.UtcNow;
        return await db.RefreshTokens.SingleOrDefaultAsync(x => x.UserId == userId && x.TokenHash == hash &&
            !x.IsDeleted && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct)
            ?? throw new AuthenticationException("The current session could not be verified. Sign in again.");
    }

    public async Task<IReadOnlyList<UserSessionResponse>> GetSessionsAsync(Guid userId, string? currentRefreshToken, CancellationToken ct = default)
    {
        await Actor(userId, ct); var current = await Current(userId, currentRefreshToken, ct); var now = DateTime.UtcNow;
        return await db.RefreshTokens.AsNoTracking().Where(x => x.UserId == userId && !x.IsDeleted && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new UserSessionResponse(x.Id, x.DeviceName, x.CreatedByIp, x.CreatedAtUtc, x.ExpiresAtUtc, x.Id == current.Id)).ToListAsync(ct);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, string? currentRefreshToken, string? ip, CancellationToken ct = default)
    {
        await Actor(userId, ct); var current = await Current(userId, currentRefreshToken, ct);
        if (current.Id == sessionId) throw new UserManagementException("Use Sign Out to end the current session.");
        var now = DateTime.UtcNow;
        var target = await db.RefreshTokens.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId &&
            !x.IsDeleted && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct)
            ?? throw new KeyNotFoundException("Session is unavailable.");
        Revoke(target, userId, ip, now); await db.SaveChangesAsync(ct);
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid userId, string? currentRefreshToken, string? ip, CancellationToken ct = default)
    {
        await Actor(userId, ct); var current = await Current(userId, currentRefreshToken, ct); var now = DateTime.UtcNow;
        var others = await db.RefreshTokens.Where(x => x.UserId == userId && x.Id != current.Id && !x.IsDeleted &&
            x.RevokedAtUtc == null && x.ExpiresAtUtc > now).ToListAsync(ct);
        foreach (var token in others) Revoke(token, userId, ip, now);
        await db.SaveChangesAsync(ct); return others.Count;
    }

    private static void Revoke(RefreshToken token, Guid actor, string? ip, DateTime now)
    {
        token.RevokedAtUtc = now; token.RevokedByIp = ip; token.RevocationReason = "Revoked from account settings.";
        token.UpdatedBy = actor; token.UpdatedAtUtc = now;
    }
}
