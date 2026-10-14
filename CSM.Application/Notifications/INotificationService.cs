using CSM.Application.Notifications.Dtos;

namespace CSM.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyCollection<NotificationResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isRead,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> CreateAsync(
        Guid currentUserId,
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> MarkReadAsync(
        Guid currentUserId,
        Guid notificationId,
        MarkNotificationReadRequest request,
        CancellationToken cancellationToken = default);
}
