namespace CSM.Application.Payroll.Calculations.Dtos;

public sealed class CalculatePayrollRequest
{
    public Guid EmployeeId { get; set; }

    public decimal OvertimeHours { get; set; }

    public int WorkingDays { get; set; }

    // Custom entries (optional)
    public decimal? PensionDeduction { get; set; }

    public decimal? TaxDeduction { get; set; }

    public decimal? OtherDeduction { get; set; }
}