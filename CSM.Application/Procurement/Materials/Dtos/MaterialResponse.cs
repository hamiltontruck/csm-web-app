using CSM.Domain.Enums;

namespace CSM.Application.Procurement.Materials.Dtos;

public sealed class MaterialResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public MaterialType MaterialType { get; set; }

    public decimal? RebarDiameterMm { get; set; }

    public string? RebarGrade { get; set; }

    public Guid? DefaultCostCodeId { get; set; }

    public string? DefaultCostCode { get; set; }

    public string? DefaultCostCodeName { get; set; }

    public decimal? StandardUnitCost { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}


