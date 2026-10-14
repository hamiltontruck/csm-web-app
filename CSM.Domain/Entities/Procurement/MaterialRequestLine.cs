using CSM.Domain.Common;

namespace CSM.Domain.Entities.Procurement;

public class MaterialRequestLine : TenantEntity
{
    public Guid MaterialRequestId { get; set; }

    public Guid MaterialId { get; set; }

    public decimal RequestedQuantity { get; set; }

    public decimal ApprovedQuantity { get; set; }

    public decimal DeliveredQuantity { get; set; }

    public string? Remarks { get; set; }

    public MaterialRequest MaterialRequest { get; set; } = null!;

    public Material Material { get; set; } = null!;
}