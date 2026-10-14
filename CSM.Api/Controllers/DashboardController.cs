using System.Security.Claims;
using CSM.Application.Dashboard;
using CSM.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IExecutiveDashboardService _dashboardService;

    public DashboardController(
        IExecutiveDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("executive")]
    public async Task<IActionResult> GetExecutiveDashboard(
        CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId();

        var dashboard = await _dashboardService.GetAsync(
            companyId,
            cancellationToken);

        return Ok(dashboard);
    }

    private Guid GetCompanyId()
    {
        var value =
            User.FindFirst("companyId")?.Value ??
            User.FindFirst("CompanyId")?.Value ??
            User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(value, out var companyId))
        {
            throw new ProjectManagementException(
                "Authenticated company identifier is invalid.");
        }

        return companyId;
    }
}