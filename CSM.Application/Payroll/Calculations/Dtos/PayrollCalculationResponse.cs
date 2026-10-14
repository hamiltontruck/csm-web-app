namespace CSM.Application.Payroll.Calculations.Dtos;

public sealed class PayrollCalculationResponse
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public decimal BasicSalary { get; set; }
    public decimal HousingAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal PositionAllowance { get; set; }

    public decimal OvertimeHours { get; set; }
    public decimal OvertimeAmount { get; set; }

    public decimal GrossSalary { get; set; }

    public decimal PensionDeduction { get; set; }
    public decimal TaxDeduction { get; set; }

    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }

    public decimal OtherDeduction { get; set; }
}