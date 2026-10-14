namespace CSM.Web.Models.Projects;

public class CreateEquipmentEntryRequest
{
    public Guid DailyProgressTaskEntryId { get; set; }

    public Guid EquipmentId { get; set; }

    public string EquipmentName { get; set; } = "";

    public decimal HoursUsed { get; set; }

    public string? OperatorName { get; set; }

    public string? Remarks { get; set; }
}