using CSM.Domain.Common;
using CSM.Domain.Entities.Identity;

namespace CSM.Domain.Entities.Finance;

public class PettyCashTransaction : AuditableEntity
{
    public Guid PettyCashAccountId { get; set; }
    public PettyCashAccount PettyCashAccount { get; set; } = null!;

    public Guid? CostCodeId { get; set; }
    public CostCode? CostCode { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public string ReferenceNumber { get; set; } = string.Empty;

    /// CashIn / CashOut
    public string TransactionType { get; set; } = string.Empty;

    public DateTime TransactionDateUtc { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? Remarks { get; set; }

    public bool IsApproved { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
}