using CSM.Domain.Common;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Equipment;

namespace CSM.Domain.Entities.Procurement;

public class Equipment : TenantEntity
{
    public string EquipmentCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public string? SerialNumber { get; set; }

    public string? RegistrationNumber { get; set; }

    public EquipmentOwnershipType OwnershipType { get; set; }

    public EquipmentStatus Status { get; set; }
        = EquipmentStatus.Available;

    public ICollection<EquipmentHourLog> HourLogs { get; set; }
        = new List<EquipmentHourLog>();

    public Guid? VendorId { get; set; }

    public decimal? PurchaseCost { get; set; }

    public decimal? RentalRate { get; set; }

    public string? RentalRateUnit { get; set; }

    public DateOnly? RentalStartDate { get; set; }

    public DateOnly? RentalEndDate { get; set; }

    public decimal CurrentMeterReading { get; set; }

    public string MeterUnit { get; set; } = "Hours";

    public bool IsActive { get; set; } = true;

    public Vendor? Vendor { get; set; }

    public ICollection<EquipmentAssignment> Assignments { get; set; }
        = new List<EquipmentAssignment>();

    public ICollection<EquipmentMaintenance> MaintenanceRecords { get; set; }
        = new List<EquipmentMaintenance>();

    public ICollection<EquipmentDowntime> DowntimeRecords { get; set; }
        = new List<EquipmentDowntime>();

    public ICollection<EquipmentFuelLog> FuelLogs { get; set; }
        = new List<EquipmentFuelLog>();
}