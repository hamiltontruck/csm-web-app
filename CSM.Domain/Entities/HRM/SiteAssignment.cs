using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.HRM;

public class SiteAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? RoleAtSite { get; set; }

    public bool IsPrimaryAssignment { get; set; }

    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;

    public ConstructionSite ConstructionSite { get; set; } = null!;
}