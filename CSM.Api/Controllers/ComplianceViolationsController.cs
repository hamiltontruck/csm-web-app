using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Violations;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Compliance.View")]
public class ComplianceViolationsController : ControllerBase
{
    private readonly ComplianceViolationService _service;

    public ComplianceViolationsController(
        ComplianceViolationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ViolationResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(
            GetCurrentCompanyId(),
            cancellationToken);

        return Ok(result);
    }

    private Guid GetCurrentCompanyId()
    {
        var value = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(value, out var companyId))
        {
            throw new EquipmentManagementException(
                "Current company identity is invalid.");
        }

        return companyId;
    }
}