namespace CSM.Web.Models.Notifications;

public sealed class NotificationResponse
{
    public Guid Id { get; set; }

    public Guid RecipientUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public string? SourceEntityType { get; set; }

    public Guid? SourceEntityId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}