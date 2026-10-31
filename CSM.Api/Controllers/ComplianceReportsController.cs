using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Reports;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Compliance.View")]
public class ComplianceReportsController : ControllerBase
{
    private readonly ComplianceReportService _service;

    public ComplianceReportsController(ComplianceReportService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ComplianceSummaryReportResponse>> Summary(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetSummaryAsync(
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