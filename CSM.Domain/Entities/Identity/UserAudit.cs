using CSM.Domain.Common;

namespace CSM.Domain.Entities.Identity;

public class UserAudit : BaseEntity
{
    // Tenant at the time of the event. Null legacy rows have no provable tenant.
    public Guid? CompanyId { get; init; }

    public Guid ActorUserId { get; set; }

    public User ActorUser { get; set; } = null!;

    public Guid TargetUserId { get; set; }

    public User TargetUser { get; set; } = null!;

    public string Action { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
}