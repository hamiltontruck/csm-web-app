namespace CSM.Application.HRM.Payroll.Dtos;

public sealed record PayrollAdjustmentResponse(
    Guid Id,
    string Type,
    string Description,
    decimal Amount,
    bool IsDeduction);
