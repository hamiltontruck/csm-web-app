using System.Text.Json.Serialization;

namespace CSM.Web.Models.Procurement;

public class MaterialResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public string MaterialCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public string Category { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";

     [JsonConverter(typeof(JsonStringEnumConverter))]
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

    [JsonIgnore]
    public string MaterialTypeName => MaterialType.ToString();
}