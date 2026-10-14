using CSM.Application.Payroll.Calculations.Dtos;

namespace CSM.Application.Payroll.Calculations;

public interface IPayrollCalculationService
{
    Task<PayrollCalculationResponse> CalculateAsync(
        CalculatePayrollRequest request,
        CancellationToken cancellationToken);
}