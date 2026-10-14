using CSM.Domain.Enums;

namespace CSM.Application.Projects.DailySiteDiaries.Dtos;

public sealed class CreateDailySiteDiaryRequest
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
}