namespace CSM.Application.Projects.Equipment.Dtos;

public sealed record EquipmentResponse(
    Guid Id,
    string EquipmentCode,
    string Name,
    string Category,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    bool IsRented,
    string? SupplierName,
    DateOnly? PurchaseDate,
    DateOnly? NextMaintenanceDate,
    bool IsActive);