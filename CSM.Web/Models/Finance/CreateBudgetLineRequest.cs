namespace CSM.Web.Models.Finance;

public sealed class CreateBudgetLineRequest
{
    public Guid BudgetId { get; set; }

    public Guid CostCodeId { get; set; }

    public string? Description { get; set; }

    public decimal BudgetedAmount { get; set; }

    public decimal RevisedAmount { get; set; }
}