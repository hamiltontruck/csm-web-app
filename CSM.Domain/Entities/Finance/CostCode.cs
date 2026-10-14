using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Finance;

public class CostCode : TenantEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? ParentCostCodeId { get; set; }

    public Guid? ConstructionSiteId { get; set; }

    public ConstructionSite? ConstructionSite { get; set; }

    public bool IsActive { get; set; } = true;

    public CostCode? ParentCostCode { get; set; }

    public ICollection<CostCode> Children { get; set; }
        = new List<CostCode>();

    public ICollection<BudgetLine> BudgetLines { get; set; }
        = new List<BudgetLine>();

    public ICollection<Expense> Expenses { get; set; }
        = new List<Expense>();

    public ICollection<InvoiceLine> InvoiceLines { get; set; }
        = new List<InvoiceLine>();
}