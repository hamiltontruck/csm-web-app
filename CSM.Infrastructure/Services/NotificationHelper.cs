using CSM.Application.Notifications;
using CSM.Application.Notifications.Dtos;
using CSM.Domain.Enums;

namespace CSM.Infrastructure.Services;

public sealed class NotificationHelper
{
    private readonly INotificationService _notifications;

    public NotificationHelper(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task SendAsync(
        Guid senderUserId,
        Guid recipientUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string? sourceEntityType = null,
        Guid? sourceEntityId = null,
        CancellationToken ct = default)
    {
        await _notifications.CreateAsync(
            senderUserId,
            new CreateNotificationRequest(
                recipientUserId,
                title,
                message,
                severity,
                sourceEntityType,
                sourceEntityId),
            ct);
    }
}