using CSM.Application.Notifications;
using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class LowStockNotificationService
    : ILowStockNotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly NotificationHelper _notificationHelper;

    public LowStockNotificationService(
        ApplicationDbContext dbContext,
        NotificationHelper notificationHelper)
    {
        _dbContext = dbContext;
        _notificationHelper = notificationHelper;
    }

    public async Task NotifyIfLowStockAsync(
        Guid stockItemId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var stockItem = await _dbContext.StockItems
            .AsNoTracking()
            .Include(x => x.Material)
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == stockItemId,
                cancellationToken);

        if (stockItem is null)
        {
            return;
        }

        if (stockItem.QuantityOnHand > stockItem.ReorderLevel)
        {
            return;
        }

        var recipientUserIds = new HashSet<Guid>();

        var administratorUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == stockItem.CompanyId &&
                x.IsActive &&
                x.UserRoles.Any(
                    ur => ur.Role.Name == AppRoles.Administrator))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in administratorUserIds)
        {
            recipientUserIds.Add(userId);
        }

        var siteOperationalUserIds = await _dbContext.UserSiteAccesses
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == stockItem.CompanyId &&
                x.SiteId == stockItem.ConstructionSiteId &&
                x.IsActive &&
                x.User.IsActive &&
                x.User.UserRoles.Any(
                    ur =>
                        ur.Role.Name == AppRoles.Storekeeper ||
                        ur.Role.Name == AppRoles.ProcurementOfficer))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in siteOperationalUserIds)
        {
            recipientUserIds.Add(userId);
        }

        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var materialName =
            stockItem.Material?.Name ?? "Unknown Material";

        var siteName =
            stockItem.ConstructionSite?.Name ?? "Unknown Site";

        var title = "Low Stock Alert";

        var message =
            $"Material {materialName} at site {siteName} " +
            $"is at or below its reorder level. " +
            $"Current stock: {stockItem.QuantityOnHand:N2}; " +
            $"reorder level: {stockItem.ReorderLevel:N2}.";

        foreach (var recipientUserId in recipientUserIds)
        {
            await _notificationHelper.SendAsync(
                actorUserId,
                recipientUserId,
                title,
                message,
                NotificationSeverity.Warning,
                "StockItem",
                stockItem.Id,
                cancellationToken);
        }
    }
}