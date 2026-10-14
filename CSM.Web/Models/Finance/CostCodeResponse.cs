namespace CSM.Web.Models.Finance;

public sealed class CostCodeResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? ConstructionSiteId { get; set; }

    public string? ParentCode { get; set; }
    public string? ParentName { get; set; }

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public Guid? ParentCostCodeId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}