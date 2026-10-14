using CSM.Application.HRM.Payroll.Dtos;

namespace CSM.Application.HRM.Payroll;

public interface IPayrollService
{
    Task<IReadOnlyCollection<PayrollResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> GetByIdAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> CreateAsync(
        Guid currentUserId,
        CreatePayrollRequest request,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> CalculateAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CalculatePayrollRequest request,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> ApproveAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        ApprovePayrollRequest request,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> MarkPaidAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        MarkPayrollPaidRequest request,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> CancelAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CancelPayrollRequest request,
        CancellationToken cancellationToken = default);

    Task<PayrollResponse> AddAdjustmentAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CreatePayrollAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> GeneratePayslipAsync(
        Guid currentUserId,
        Guid payrollRecordId,
        CancellationToken cancellationToken = default);
}