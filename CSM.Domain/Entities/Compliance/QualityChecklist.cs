using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Compliance;

public class QualityChecklist : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }
    public ConstructionSite ConstructionSite { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<QualityChecklistItem> Items { get; set; }
        = new List<QualityChecklistItem>();
}