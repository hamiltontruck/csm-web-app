using CSM.Web.Models.Notifications;

namespace CSM.Web.Services;

public interface INotificationApiService
{
    Task<IReadOnlyList<NotificationResponse>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> MarkReadAsync(
        Guid notificationId,
        bool isRead = true,
        CancellationToken cancellationToken = default);
}