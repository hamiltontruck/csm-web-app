namespace CSM.Web.Models.Equipment;

public class EquipmentUtilizationResponse
{
    public Guid EquipmentId { get; set; }

    public string EquipmentCode { get; set; } = string.Empty;

    public string EquipmentName { get; set; } = string.Empty;

    public decimal HoursWorked { get; set; }

    public decimal FuelConsumed { get; set; }

    public decimal DowntimeHours { get; set; }

    public decimal MaintenanceCost { get; set; }
}