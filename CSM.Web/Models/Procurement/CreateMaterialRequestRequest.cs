public class CreateMaterialRequestRequest
{
    public Guid ConstructionSiteId { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public DateOnly RequestDate { get; set; }

    public DateTime? RequiredByDate { get; set; }

    public string? Purpose { get; set; }

    public string Priority { get; set; } = "Normal";

    public List<MaterialRequestLineRequest> Lines { get; set; } = new();
}

public class MaterialRequestLineRequest
{
    public Guid MaterialId { get; set; }

    public decimal RequestedQuantity { get; set; }

    public string? Remarks { get; set; }
}