using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Dashboard;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ComplianceDashboardController : ControllerBase
{
    private readonly ComplianceDashboardService _service;

    public ComplianceDashboardController(ComplianceDashboardService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ComplianceDashboardResponse>> Get(
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