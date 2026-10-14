using CSM.Domain.Enums;

namespace CSM.Application.Procurement.Materials.Dtos;

public sealed class UpdateMaterialRequest
{
    public string MaterialCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public MaterialType MaterialType { get; set; } = MaterialType.Bulk;

    public Guid? DefaultCostCodeId { get; set; }

    public decimal? StandardUnitCost { get; set; }

    public bool IsActive { get; set; }
}

