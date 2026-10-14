using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Identity;

public class UserSiteAccess : AuditableEntity
{
    public Guid CompanyId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid SiteId { get; set; }
    public ConstructionSite Site { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}