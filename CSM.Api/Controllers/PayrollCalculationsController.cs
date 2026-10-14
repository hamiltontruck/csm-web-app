using CSM.Application.Common.Security;
using CSM.Application.Payroll.Calculations;
using CSM.Application.Payroll.Calculations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/payroll/calculations")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class PayrollCalculationsController : ControllerBase
{
    private readonly IPayrollCalculationService _service;

    public PayrollCalculationsController(
        IPayrollCalculationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<PayrollCalculationResponse>> Calculate(
        CalculatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CalculateAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}