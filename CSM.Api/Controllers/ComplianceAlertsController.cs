using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Alerts;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Compliance.View")]
public class ComplianceAlertsController : ControllerBase
{
    private readonly ComplianceAlertService _service;

    public ComplianceAlertsController(ComplianceAlertService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ComplianceAlertResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(
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