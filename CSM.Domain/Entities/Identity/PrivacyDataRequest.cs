
using CSM.Domain.Common;

namespace CSM.Domain.Entities.Identity;

public sealed class PrivacyDataRequest : TenantEntity
{
    public Guid UserId { get; set; }

    public string RequestType { get; set; } = string.Empty;

    public string Status { get; set; } = "Submitted";

    public string? Description { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public string? ResolutionNotes { get; set; }
}
