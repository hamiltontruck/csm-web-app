using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public sealed class Equipment : BaseEntity
{
    public string EquipmentCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? SerialNumber { get; set; }

    public bool IsRented { get; set; }

    public string? SupplierName { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? NextMaintenanceDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<EquipmentAssignment> Assignments { get; set; }
        = new List<EquipmentAssignment>();
}