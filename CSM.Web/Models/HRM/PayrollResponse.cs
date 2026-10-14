namespace CSM.Web.Models.HRM;

public sealed class PayrollResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

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

    public string CurrencyCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentReference { get; set; }

    public IReadOnlyCollection<PayrollAdjustmentResponse> Adjustments { get; set; }
        = [];

    public DateTime CreatedAtUtc { get; set; }
}