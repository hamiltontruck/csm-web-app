namespace CSM.Web.Models.Equipment;

public sealed class EquipmentFuelLogResponse
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }

    public string EquipmentCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;

    public Guid? SiteId { get; set; }
    public string? SiteName { get; set; }

    public DateTime RefueledAtUtc { get; set; }

    public decimal Liters { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalCost { get; set; }

    public decimal MeterReading { get; set; }

    public string FuelType { get; set; } = "Diesel";

    public string? SupplierName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Remarks { get; set; }
}