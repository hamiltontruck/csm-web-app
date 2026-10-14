using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public class DailyProgressLog : TenantEntity
{
    public Guid ProjectId { get; set; }

    public DateOnly LogDate { get; set; }

    public string? WeatherCondition { get; set; }

    public string LogNumber { get; set; } = string.Empty;

    public decimal? TemperatureCelsius { get; set; }

    public int LaborCount { get; set; }

    public int ContractorLaborCount { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string? WorkCompleted { get; set; }

    public string? WorkPlannedNext { get; set; }

    public string? MaterialsUsedSummary { get; set; }

    public string? EquipmentUsedSummary { get; set; }

    public string? Delays { get; set; }

    public string? Issues { get; set; }

    public string? SafetyNotes { get; set; }

    public string? Remarks { get; set; }

    public Project Project { get; set; } = null!;

    public ICollection<DailyProgressTaskEntry> DailyProgressTaskEntries { get; set; }
                = new List<DailyProgressTaskEntry>();


}