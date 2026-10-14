using CSM.Domain.Common;

namespace CSM.Domain.Entities.Organization;

public class BusinessUnit : TenantEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;

    public ICollection<ConstructionSite> ConstructionSites { get; set; }
        = new List<ConstructionSite>();
}