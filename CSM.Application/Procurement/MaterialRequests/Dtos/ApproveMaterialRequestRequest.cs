namespace CSM.Application.Procurement.MaterialRequests.Dtos;

public sealed class ApproveMaterialRequestRequest
{
    public string? Remarks { get; set; }

    public IReadOnlyCollection<MaterialRequestApprovalLine> Lines { get; set; }
        = Array.Empty<MaterialRequestApprovalLine>();
}
