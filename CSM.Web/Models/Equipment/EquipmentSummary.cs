namespace CSM.Web.Models.Equipment;

public class EquipmentSummary
{
    public Guid Id { get; set; }

    public string EquipmentCode { get; set; } = "";

    public string Name { get; set; } = "";

    public string Status { get; set; } = "";
}