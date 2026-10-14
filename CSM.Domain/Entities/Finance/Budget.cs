using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Finance;

public class Budget : TenantEntity
{
    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string BudgetNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }

    public ICollection<BudgetLine> Lines { get; set; }
        = new List<BudgetLine>();
}