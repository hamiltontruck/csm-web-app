using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Procurement;

public class EquipmentDowntime : TenantEntity
{
    public Guid EquipmentId { get; set; }

    public Guid? ConstructionSiteId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Resolution { get; set; }

    public string DowntimeNumber { get; set; } = string.Empty;

    public Equipment Equipment { get; set; } = null!;

    public ConstructionSite? ConstructionSite { get; set; }
}