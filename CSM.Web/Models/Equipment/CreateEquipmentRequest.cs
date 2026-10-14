namespace CSM.Web.Models.Equipment;

public class CreateEquipmentRequest
{
    public string EquipmentCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";

    public string? Make { get; set; }
    public string? Model { get; set; }

    public string? SerialNumber { get; set; }
    public string? RegistrationNumber { get; set; }

    public string OwnershipType { get; set; } = "Owned";

    public Guid? VendorId { get; set; }

    public decimal? PurchaseCost { get; set; }
    public decimal? RentalRate { get; set; }

    public string? RentalRateUnit { get; set; }

    public DateOnly? RentalStartDate { get; set; }
    public DateOnly? RentalEndDate { get; set; }

    public decimal CurrentMeterReading { get; set; }

    public string MeterUnit { get; set; } = "Hours";
}