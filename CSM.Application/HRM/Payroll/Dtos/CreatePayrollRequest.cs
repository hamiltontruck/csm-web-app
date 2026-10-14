namespace CSM.Application.HRM.Payroll.Dtos;

public sealed record CreatePayrollRequest(
    Guid EmployeeId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd);
