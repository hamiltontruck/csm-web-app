using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Application.Common.Exceptions;
using CSM.Application.Notifications;
using CSM.Application.Notifications.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Notifications;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<NotificationResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isRead,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == actor.CompanyId &&
                    x.RecipientUserId == currentUserId);
        query = await VisibleToActorAsync(actor, query, cancellationToken);

        if (isRead.HasValue)
        {
            query = query.Where(
                x => x.IsRead == isRead.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(
                x =>
                    new NotificationResponse(
                        x.Id,
                        x.RecipientUserId,
                        x.Title,
                        x.Message,
                        x.Severity,
                        x.IsRead,
                        x.ReadAtUtc,
                        x.SourceEntityType,
                        x.SourceEntityId,
                        x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var query = _dbContext.Notifications.AsNoTracking().Where(x =>
            x.CompanyId == actor.CompanyId && x.RecipientUserId == currentUserId && !x.IsRead);
        query = await VisibleToActorAsync(actor, query, cancellationToken);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<NotificationResponse> CreateAsync(
        Guid currentUserId,
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        if (!await new PermissionEvaluator(_dbContext).HasPermissionAsync(currentUserId, "Notifications_Manage", cancellationToken))
            throw new UnauthorizedAccessException("Notification management is not permitted.");

        if (!actor.CompanyId.HasValue)
        {
            throw new NotificationManagementException(
                "Current user is not assigned to a company.");
        }

        var recipient = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == request.RecipientUserId &&
                    x.CompanyId == actor.CompanyId &&
                    x.IsActive,
                cancellationToken);

        if (recipient is null)
        {
            throw new NotificationManagementException(
                "Notification recipient was not found in the current company.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new NotificationManagementException(
                "Notification title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new NotificationManagementException(
                "Notification message is required.");
        }

        var notification = new Notification
        {
            CompanyId = actor.CompanyId.Value,
            RecipientUserId = recipient.Id,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Severity = request.Severity,
            SourceEntityType =
                string.IsNullOrWhiteSpace(request.SourceEntityType)
                    ? null
                    : request.SourceEntityType.Trim(),
            SourceEntityId = request.SourceEntityId,
            IsRead = false
        };

        _dbContext.Notifications.Add(notification);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return ToResponse(notification);
    }

    public async Task<NotificationResponse> MarkReadAsync(
        Guid currentUserId,
        Guid notificationId,
        MarkNotificationReadRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var query = _dbContext.Notifications.Where(x => x.Id == notificationId &&
            x.CompanyId == actor.CompanyId && x.RecipientUserId == currentUserId);
        query = await VisibleToActorAsync(actor, query, cancellationToken);
        // Scope subqueries contain AsNoTracking; explicitly track this authorized
        // notification so a successful read-state response is persisted too.
        var notification = await query.AsTracking().SingleOrDefaultAsync(cancellationToken);

        if (notification is null)
        {
            throw new NotificationManagementException(
                "Notification was not found.");
        }

        notification.IsRead = request.IsRead;
        notification.ReadAtUtc =
            request.IsRead
                ? DateTime.UtcNow
                : null;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return ToResponse(notification);
    }

    private async Task<IQueryable<Notification>> VisibleToActorAsync(User actor, IQueryable<Notification> query, CancellationToken cancellationToken)
    {
        var independentRoles = await _dbContext.UserRoles.Where(x => x.UserId == actor.Id && x.Role.Name != "ProcurementOfficer")
            .Select(x => x.Role.Name).Distinct().ToArrayAsync(cancellationToken);
        var scope = await ProcurementOfficerScope.ResolveAsync(_dbContext, actor.Id,
            GranularPermission.Notifications_View, cancellationToken,
            message => new NotificationManagementException(message), independentRoles);
        if (!scope.IsScoped) return query;
        actor = scope.For(actor);
        bool Has(GranularPermission permission) => actor.UserRoles.Any(r => r.Role.GranularPermissions.Any(p => p.Permission == permission));
        var canRequests = Has(GranularPermission.Procurement_MaterialRequest_View);
        var canOrders = Has(GranularPermission.Procurement_PurchaseOrder_View);
        var canStock = Has(GranularPermission.Procurement_Inventory_View);
        var requests = scope.Filter(_dbContext.MaterialRequests, x => x.CompanyId, x => (Guid?)x.ConstructionSiteId).Where(x => canRequests).Select(x => x.Id);
        var orders = scope.Filter(_dbContext.PurchaseOrders, x => x.CompanyId, x => (Guid?)x.ConstructionSiteId).Where(x => canOrders).Select(x => x.Id);
        var receipts = scope.Filter(_dbContext.GoodsReceipts, x => x.CompanyId, x => (Guid?)x.ConstructionSiteId).Where(x => canStock).Select(x => x.Id);
        var stocks = scope.Filter(_dbContext.StockItems, x => x.CompanyId, x => (Guid?)x.ConstructionSiteId).Where(x => canStock).Select(x => x.Id);
        var movements = scope.FilterStockMovements(_dbContext, _dbContext.StockMovements).Where(x => canStock).Select(x => x.Id);
        return query.Where(x =>
            (x.SourceEntityType != "MaterialRequest" || (x.SourceEntityId.HasValue && requests.Contains(x.SourceEntityId.Value))) &&
            (x.SourceEntityType != "PurchaseOrder" || (x.SourceEntityId.HasValue && orders.Contains(x.SourceEntityId.Value))) &&
            (x.SourceEntityType != "GoodsReceipt" || (x.SourceEntityId.HasValue && receipts.Contains(x.SourceEntityId.Value))) &&
            (x.SourceEntityType != "StockItem" || (x.SourceEntityId.HasValue && stocks.Contains(x.SourceEntityId.Value))) &&
            (!(x.SourceEntityType == "StockMovement" || x.SourceEntityType == "StockIssue" || x.SourceEntityType == "StockReturn" || x.SourceEntityType == "StockAdjustment") ||
                (x.SourceEntityId.HasValue && movements.Contains(x.SourceEntityId.Value))));
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (!await new PermissionEvaluator(_dbContext).HasPermissionAsync(currentUserId, "Notifications_View", cancellationToken))
            throw new UnauthorizedAccessException("Notification access is not permitted.");

        return await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId && x.IsActive && !x.IsDeleted,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Notification access is not permitted.");
    }

    private static NotificationResponse ToResponse(
        Notification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.RecipientUserId,
            notification.Title,
            notification.Message,
            notification.Severity,
            notification.IsRead,
            notification.ReadAtUtc,
            notification.SourceEntityType,
            notification.SourceEntityId,
            notification.CreatedAtUtc);
    }
}


