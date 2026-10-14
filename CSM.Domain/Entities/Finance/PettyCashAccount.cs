using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Finance;

public class PettyCashAccount : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid? ConstructionSiteId { get; set; }
    public ConstructionSite? ConstructionSite { get; set; }

    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PettyCashTransaction> Transactions { get; set; }
        = new List<PettyCashTransaction>();
}