using CSM.Domain.Common;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Notifications;

public sealed class Notification : TenantEntity
{
    public Guid RecipientUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public string? SourceEntityType { get; set; }

    public Guid? SourceEntityId { get; set; }

    public User RecipientUser { get; set; } = null!;
}