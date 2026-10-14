using CSM.Domain.Common;

namespace CSM.Domain.Entities.Compliance;

public sealed class DocumentCategory : TenantEntity
{
    public string CategoryCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
