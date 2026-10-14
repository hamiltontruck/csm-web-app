namespace CSM.Web.Models.Equipment;

public class CloseEquipmentDowntimeRequest
{
    public DateTime EndedAtUtc { get; set; }

    public string? Resolution { get; set; }
}