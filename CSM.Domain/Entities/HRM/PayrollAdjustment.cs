using CSM.Domain.Common;

namespace CSM.Domain.Entities.HRM;

public class PayrollAdjustment : TenantEntity
{
    public Guid PayrollRecordId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public bool IsDeduction { get; set; }

    public PayrollRecord PayrollRecord { get; set; } = null!;
}