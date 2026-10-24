
namespace CSM.Web.Models.Procurement;

public sealed class UpdateMaterialRequestRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public DateOnly RequestDate { get; set; }

    public DateOnly? RequiredByDate { get; set; }

    public string? Purpose { get; set; }

    public string Priority { get; set; } = "Normal";

    public IReadOnlyCollection<MaterialRequestLineInput> Lines { get; set; }
        = Array.Empty<MaterialRequestLineInput>();
}

public sealed class MaterialRequestLineInput
{
    public Guid MaterialId { get; set; }

    public decimal RequestedQuantity { get; set; }

    public string? Remarks { get; set; }
}
