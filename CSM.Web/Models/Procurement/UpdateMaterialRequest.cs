namespace CSM.Web.Models.Procurement;

public class UpdateMaterialRequest
{
    public string MaterialCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public string Category { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";

    public MaterialType MaterialType { get; set; } = MaterialType.Bulk;

    public Guid? DefaultCostCodeId { get; set; }
    public decimal? StandardUnitCost { get; set; }

    public bool IsActive { get; set; }
}