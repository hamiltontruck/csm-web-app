namespace CSM.Web.Models.Procurement;

public sealed class ApproveMaterialRequestRequest
{
    public string? Remarks { get; set; }
    public List<MaterialRequestApprovalLine> Lines { get; set; } = [];
}

public sealed class MaterialRequestApprovalLine
{
    public Guid MaterialRequestLineId { get; set; }
    public decimal ApprovedQuantity { get; set; }
}
