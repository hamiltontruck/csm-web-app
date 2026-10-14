using CSM.Domain.Common;

namespace CSM.Domain.Entities.Procurement;

public class RebarSpec : TenantEntity
{
    public Guid MaterialId { get; set; }

    public decimal DiameterMm { get; set; }

    public string Grade { get; set; } = string.Empty;

    public Material Material { get; set; } = null!;
}
