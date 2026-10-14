namespace CSM.Web.Models.Finance;

public sealed class BudgetResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public Guid? ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = "";

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public string BudgetNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "";

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public string Status { get; set; } = "";

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}