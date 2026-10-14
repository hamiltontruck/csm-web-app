using CSM.Domain.Common;

namespace CSM.Domain.Entities.Audit;

public class AuditLog : TenantEntity
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public new DateTime CreatedAtUtc { get; set; }

    public string? IpAddress { get; set; }
}