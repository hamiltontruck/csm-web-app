namespace CSM.Web.Models.Equipment;

public class UpdateEquipmentDowntimeRequest
{
    public Guid? ConstructionSiteId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Resolution { get; set; }
}