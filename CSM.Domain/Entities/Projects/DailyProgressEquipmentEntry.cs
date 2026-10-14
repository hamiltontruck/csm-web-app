using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public class DailyProgressEquipmentEntry : TenantEntity
{
    public Guid DailyProgressTaskEntryId { get; set; }
    public DailyProgressTaskEntry DailyProgressTaskEntry { get; set; } = null!;

    public Guid EquipmentId { get; set; }

    public string EquipmentName { get; set; } = "";

    public decimal HoursUsed { get; set; }

    public string? OperatorName { get; set; }

    public string? Remarks { get; set; }
}