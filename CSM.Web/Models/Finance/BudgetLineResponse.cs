namespace CSM.Web.Models.Finance;

public sealed class BudgetLineResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BudgetId { get; set; }

    public Guid CostCodeId { get; set; }

    public string CostCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal BudgetedAmount { get; set; }

    public decimal RevisedAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}