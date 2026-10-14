namespace CSM.Application.HRM.Payroll.Dtos;

public sealed record CreatePayrollAdjustmentRequest(
    string Type,
    string Description,
    decimal Amount,
    bool IsDeduction);
