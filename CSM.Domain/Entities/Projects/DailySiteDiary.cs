using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Projects;

namespace CSM.Domain.Entities.Projects;

public sealed class DailySiteDiary : TenantEntity
{
    public Guid SiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public DateOnly DiaryDate { get; set; }

    public WeatherType Weather { get; set; }

    public decimal Temperature { get; set; }

    public int WorkforceCount { get; set; }

    public int EquipmentInUse { get; set; }

    public string WorkPerformed { get; set; } = string.Empty;

    public string? DelayReason { get; set; }

    public string? SafetyObservation { get; set; }

    public ConstructionSite Site { get; set; } = null!;

    public Project? Project { get; set; }

    public ICollection<DailyProgressPhoto> ProgressPhotos { get; set; }
        = new List<DailyProgressPhoto>();
}