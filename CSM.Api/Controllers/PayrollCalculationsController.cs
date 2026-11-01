using System.Security.Claims;
using CSM.Application.Payroll.Calculations;
using CSM.Application.Payroll.Calculations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/payroll/calculations")]
[Authorize(Policy = "HRM.Payroll.View")]
[Authorize(Roles = "HrOfficer,HROfficer,SuperAdmin,CompanyAdmin")]
public sealed class PayrollCalculationsController : ControllerBase
{
    private readonly IPayrollCalculationService _service;

    public PayrollCalculationsController(IPayrollCalculationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<PayrollCalculationResponse>> Calculate(
        CalculatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
                return Forbid();
            return Ok(await _service.CalculateAsync(id, request, cancellationToken));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
