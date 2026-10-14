using CSM.Domain.Common;

namespace CSM.Domain.Entities.Finance;

public class BudgetLine : TenantEntity
{
    public Guid BudgetId { get; set; }

    public Guid CostCodeId { get; set; }

    public string? Description { get; set; }

    public decimal BudgetedAmount { get; set; }

    public decimal RevisedAmount { get; set; }

    public Budget Budget { get; set; } = null!;

    public CostCode CostCode { get; set; } = null!;
}