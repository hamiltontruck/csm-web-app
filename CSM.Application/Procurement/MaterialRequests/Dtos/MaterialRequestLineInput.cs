namespace CSM.Application.Procurement.MaterialRequests.Dtos;

public sealed class MaterialRequestLineInput
{
    public Guid MaterialId { get; set; }

    public decimal RequestedQuantity { get; set; }

    public string? Remarks { get; set; }
}
