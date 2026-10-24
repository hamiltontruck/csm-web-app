using CSM.Application.Notifications;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Notifications;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationPublisher(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void StageForUser(
        Guid companyId,
        Guid recipientUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string sourceEntityType,
        Guid sourceEntityId)
    {
        _dbContext.Notifications.Add(new Notification
        {
            CompanyId = companyId,
            RecipientUserId = recipientUserId,
            Title = title.Trim(),
            Message = message.Trim(),
            Severity = severity,
            SourceEntityType = sourceEntityType,
            SourceEntityId = sourceEntityId,
            IsRead = false
        });
    }

    public async Task StageForCompanyPermissionHoldersAsync(
        Guid companyId,
        GranularPermission permission,
        Guid? excludeUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string sourceEntityType,
        Guid sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        var holders = await GetHoldersAsync(
            companyId,
            permission,
            excludeUserId,
            cancellationToken);

        foreach (var holder in holders)
        {
            StageForUser(
                companyId,
                holder.Id,
                title,
                message,
                severity,
                sourceEntityType,
                sourceEntityId);
        }
    }

    public async Task StageForPermissionHoldersAsync(
        Guid companyId,
        Guid? siteId,
        GranularPermission permission,
        Guid? excludeUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string sourceEntityType,
        Guid sourceEntityId,
        CancellationToken cancellationToken = default)
    {
        var holders = await GetHoldersAsync(
            companyId,
            permission,
            excludeUserId,
            cancellationToken);

        if (holders.Count == 0)
        {
            return;
        }

        List<Guid> recipients;

        if (siteId is null)
        {
            // Company-level record: Site Managers cannot open these.
            recipients = holders
                .Where(h => !h.Roles.Contains(AppRoles.SiteManager))
                .Select(h => h.Id)
                .ToList();
        }
        else
        {
            var holderIds = holders.Select(h => h.Id).ToList();

            var sitesByUser = (await _dbContext.UserSiteAccesses
                    .AsNoTracking()
                    .Where(x =>
                        holderIds.Contains(x.UserId) &&
                        x.CompanyId == companyId &&
                        x.IsActive)
                    .Select(x => new { x.UserId, x.SiteId })
                    .ToListAsync(cancellationToken))
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.SiteId).ToHashSet());

            var site = siteId.Value;

            recipients = holders
                .Where(h =>
                    h.Roles.Contains(AppRoles.SuperAdmin) ||
                    h.Roles.Contains(AppRoles.CompanyAdmin) ||
                    h.Roles.Contains(AppRoles.Accountant) ||
                    !sitesByUser.TryGetValue(h.Id, out var assigned) ||
                    assigned.Contains(site))
                .Select(h => h.Id)
                .ToList();
        }

        foreach (var recipientId in recipients)
        {
            StageForUser(
                companyId,
                recipientId,
                title,
                message,
                severity,
                sourceEntityType,
                sourceEntityId);
        }
    }

    private sealed record Holder(Guid Id, List<string> Roles);

    private async Task<List<Holder>> GetHoldersAsync(
        Guid companyId,
        GranularPermission permission,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u =>
                u.CompanyId == companyId &&
                u.IsActive &&
                (excludeUserId == null || u.Id != excludeUserId) &&
                u.UserRoles.Any(ur =>
                    ur.Role.GranularPermissions.Any(
                        p => p.Permission == permission)))
            .Select(u => new Holder(
                u.Id,
                u.UserRoles
                    .Select(ur => ur.Role.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
