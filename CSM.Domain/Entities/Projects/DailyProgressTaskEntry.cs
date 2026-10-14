using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public class DailyProgressTaskEntry : TenantEntity
{
    public Guid DailyProgressLogId { get; set; }
    public DailyProgressLog DailyProgressLog { get; set; } = null!;

    public Guid WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;

    public decimal ProgressPercentage { get; set; }

    public int LabourCount { get; set; }

    public decimal EquipmentHours { get; set; }

    public string WorkCompleted { get; set; } = "";

    public string? Remarks { get; set; }

    public ICollection<DailyProgressEquipmentEntry> EquipmentEntries { get; set; }
        = new List<DailyProgressEquipmentEntry>();
}