using CSM.Domain.Enums;

namespace CSM.Application.Procurement.Equipment.Dtos;

public sealed class UpdateEquipmentRequest
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
    public Guid? VendorId { get; set; }
    public decimal? PurchaseCost { get; set; }
    public decimal? RentalRate { get; set; }
    public string? RentalRateUnit { get; set; }
    public DateOnly? RentalStartDate { get; set; }
    public DateOnly? RentalEndDate { get; set; }
    public decimal CurrentMeterReading { get; set; }
    public string MeterUnit { get; set; } = "Hours";
    public bool IsActive { get; set; } = true;
}
