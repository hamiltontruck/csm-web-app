namespace CSM.Application.Notifications;

public interface ILowStockNotificationService
{
    Task NotifyIfLowStockAsync(
        Guid stockItemId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}