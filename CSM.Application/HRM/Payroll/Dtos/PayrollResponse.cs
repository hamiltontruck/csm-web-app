using CSM.Domain.Enums;

namespace CSM.Application.HRM.Payroll.Dtos;

public sealed record PayrollResponse(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal BasePay,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal OvertimePay,
    decimal Allowances,
    decimal Bonuses,
    decimal GrossPay,
    decimal TaxDeduction,
    decimal PensionDeduction,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal NetPay,
    string CurrencyCode,
    PayrollStatus Status,
    Guid? ApprovedBy,
    DateTime? ApprovedAtUtc,
    DateTime? PaidAtUtc,
    string? PaymentReference,
    IReadOnlyCollection<PayrollAdjustmentResponse> Adjustments,
    DateTime CreatedAtUtc);
