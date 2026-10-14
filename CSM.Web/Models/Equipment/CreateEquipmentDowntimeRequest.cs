namespace CSM.Web.Models.Equipment;

public class CreateEquipmentDowntimeRequest
{
    public Guid EquipmentId { get; set; }

    public Guid? ConstructionSiteId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public string DowntimeNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? Resolution { get; set; }
}