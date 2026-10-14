using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface IPayrollService
{
    Task<IReadOnlyCollection<PayrollResponse>> GetAllAsync(
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null);

    Task<PayrollResponse?> GetByIdAsync(Guid payrollRecordId);

    Task<PayrollResponse> CreateAsync(CreatePayrollRequest request);

    Task<PayrollResponse> CalculateAsync(
        Guid payrollRecordId,
        CalculatePayrollRequest request);

    Task<PayrollResponse> ApproveAsync(
        Guid payrollRecordId,
        ApprovePayrollRequest request);

    Task<PayrollResponse> MarkPaidAsync(
        Guid payrollRecordId,
        MarkPayrollPaidRequest request);

    Task<PayrollResponse> CancelAsync(
        Guid payrollRecordId,
        CancelPayrollRequest request);

    Task<PayrollResponse> AddAdjustmentAsync(
        Guid payrollRecordId,
        CreatePayrollAdjustmentRequest request);

    Task<byte[]> GeneratePayslipAsync(Guid payrollRecordId);
}