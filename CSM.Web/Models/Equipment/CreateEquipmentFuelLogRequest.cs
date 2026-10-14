namespace CSM.Web.Models.Equipment;

public sealed class CreateEquipmentFuelLogRequest
{
    public Guid EquipmentId { get; set; }

    public DateTime RefueledAtUtc { get; set; }

    public decimal Liters { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal MeterReading { get; set; }

    public string FuelType { get; set; } = "Diesel";

    public string? SupplierName { get; set; }

    public string? InvoiceNumber { get; set; }

    public string? Remarks { get; set; }
}