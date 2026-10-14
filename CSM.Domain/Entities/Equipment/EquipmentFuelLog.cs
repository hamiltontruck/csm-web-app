using CSM.Domain.Common;

namespace CSM.Domain.Entities.Equipment;

public class EquipmentFuelLog : TenantEntity
{
    public Guid EquipmentId { get; set; }

    public global::CSM.Domain.Entities.Procurement.Equipment Equipment { get; set; } = null!;

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