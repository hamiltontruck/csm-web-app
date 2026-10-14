namespace CSM.Application.Payroll.Dtos;

public sealed class UpdateSalaryStructureRequest
{
    public Guid EmployeeId { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal HousingAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal PositionAllowance { get; set; }

    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public decimal PensionPercentage { get; set; } = 7m;

    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}