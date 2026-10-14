namespace CSM.Application.Procurement.MaterialRequests.Dtos;

public sealed class MaterialRequestLineResponse
{
    public Guid Id { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal RequestedQuantity { get; set; }

    public decimal ApprovedQuantity { get; set; }

    public decimal DeliveredQuantity { get; set; }

    public string? Remarks { get; set; }
}
