using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Compliance;

public class Permit : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string PermitNumber { get; set; } = string.Empty;

    public string PermitType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? IssuingAuthority { get; set; }

    public DateOnly? ApplicationDate { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public PermitStatus Status { get; set; } = PermitStatus.Draft;

    public int ExpiryAlertDays { get; set; } = 30;

    public string? Conditions { get; set; }

    public string? Notes { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }
}