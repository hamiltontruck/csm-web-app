using CSM.Domain.Enums;

namespace CSM.Application.Notifications.Dtos;

public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    string Title,
    string Message,
    NotificationSeverity Severity,
    string? SourceEntityType,
    Guid? SourceEntityId);