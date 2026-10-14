using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Projects.Equipment.Dtos;

public sealed class CreateEquipmentRequest
{
    [Required]
    [MaxLength(50)]
    public string EquipmentCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? SerialNumber { get; set; }

    public bool IsRented { get; set; }

    public string? SupplierName { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? NextMaintenanceDate { get; set; }
}