using System.Text.Json.Serialization;

namespace CSM.Web.Models.Procurement;

public class MaterialRequestResponse
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

    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public Guid RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalRemarks { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public List<MaterialRequestLineResponse> Lines { get; set; } = new();
}

public class MaterialRequestLineResponse
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