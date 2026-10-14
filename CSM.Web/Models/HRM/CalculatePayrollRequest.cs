namespace CSM.Web.Models.HRM;

public sealed class CalculatePayrollRequest
{
    public decimal OvertimeMultiplier { get; set; }

    public decimal Allowances { get; set; }

    public decimal Bonuses { get; set; }

    public decimal TaxDeduction { get; set; }

    public decimal PensionDeduction { get; set; }

    public decimal OtherDeductions { get; set; }
}