using CSM.Domain.Common;

namespace CSM.Domain.Entities.Identity;

public class LoginHistoryEntry : AuditableEntity
{
    public Guid UserId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public bool Succeeded { get; set; }

    public string? IpAddress { get; set; }

    public string? DeviceName { get; set; }

    public string? FailureReason { get; set; }

    public User User { get; set; } = null!;
}