namespace CSM.Application.Payroll.Dtos;

public sealed class SalaryStructureDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal HousingAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal PositionAllowance { get; set; }

    public decimal OvertimeMultiplier { get; set; }
    public decimal PensionPercentage { get; set; }

    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}