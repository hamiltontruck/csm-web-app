using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Procurement.EquipmentFuelLogs;

public sealed class UpdateEquipmentFuelLogRequest
{
    [Required]
    public DateTime RefueledAtUtc { get; set; }

    [Range(0.01, 999999)]
    public decimal Liters { get; set; }

    [Range(0.01, 999999)]
    public decimal UnitPrice { get; set; }

    [Range(0, 9999999)]
    public decimal MeterReading { get; set; }

    [Required]
    [MaxLength(30)]
    public string FuelType { get; set; } = "Diesel";

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}