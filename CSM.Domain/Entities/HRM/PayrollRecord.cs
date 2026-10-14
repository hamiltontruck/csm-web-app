using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.HRM;

public class PayrollRecord : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal BasePay { get; set; }

    public decimal RegularHours { get; set; }

    public decimal OvertimeHours { get; set; }

    public decimal OvertimePay { get; set; }

    public decimal Allowances { get; set; }

    public decimal Bonuses { get; set; }

    public decimal GrossPay { get; set; }

    public decimal TaxDeduction { get; set; }

    public decimal PensionDeduction { get; set; }

    public decimal OtherDeductions { get; set; }

    public decimal TotalDeductions { get; set; }

    public decimal NetPay { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public string? PaymentReference { get; set; }

    public Employee Employee { get; set; } = null!;

    public ICollection<PayrollAdjustment> Adjustments { get; set; }
        = new List<PayrollAdjustment>();
}