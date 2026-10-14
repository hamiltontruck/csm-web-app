using CSM.Domain.Enums;

namespace CSM.Application.Procurement.MaterialRequests.Dtos;

public sealed class MaterialRequestResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string RequestedByName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string? ProjectName { get; set; }



    public string RequestNumber { get; set; } = string.Empty;

    public DateOnly RequestDate { get; set; }

    public DateOnly? RequiredByDate { get; set; }

    public string? Purpose { get; set; }

    public Priority Priority { get; set; }

    public MaterialRequestStatus Status { get; set; }

    public Guid RequestedBy { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ApprovalRemarks { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<MaterialRequestLineResponse> Lines { get; set; }
        = Array.Empty<MaterialRequestLineResponse>();
}
