using CSM.Domain.Enums;

namespace CSM.Application.Notifications.Dtos;

public sealed record NotificationResponse(
    Guid Id,
    Guid RecipientUserId,
    string Title,
    string Message,
    NotificationSeverity Severity,
    bool IsRead,
    DateTime? ReadAtUtc,
    string? SourceEntityType,
    Guid? SourceEntityId,
    DateTime CreatedAtUtc);