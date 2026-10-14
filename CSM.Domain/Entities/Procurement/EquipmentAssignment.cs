using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;

namespace CSM.Domain.Entities.Procurement;

public class EquipmentAssignment : TenantEntity
{
    public Guid EquipmentId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }

    public decimal? MeterReadingAtAssignment { get; set; }

    public decimal? MeterReadingAtRelease { get; set; }

    public string? Remarks { get; set; }

    public string AssignmentNumber { get; set; } = string.Empty;

    public Equipment Equipment { get; set; } = null!;

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }
}