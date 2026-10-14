namespace CSM.Web.Models.OfflineSync;

public sealed class OfflineSyncOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string OperationType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int RetryCount { get; set; }

    public string Status { get; set; } = OfflineSyncStatus.Pending;

    public string? LastError { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }
}