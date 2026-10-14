namespace CSM.Application.Procurement.MaterialRequests.Dtos;

public sealed class MaterialRequestApprovalLine
{
    public Guid MaterialRequestLineId { get; set; }

    public decimal ApprovedQuantity { get; set; }
}
