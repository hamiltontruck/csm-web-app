using CSM.Application.Dashboard;
using CSM.Application.CompanySettings;
using CSM.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IExecutiveDashboardService _dashboardService;
    private readonly ICompanySettingsService _companies;

    public DashboardController(
        IExecutiveDashboardService dashboardService,
        ICompanySettingsService companies)
    {
        _dashboardService = dashboardService;
        _companies = companies;
    }

    [HttpGet("executive")]
    public async Task<IActionResult> GetExecutiveDashboard(
        CancellationToken cancellationToken,
        [FromQuery] Guid? selectedCompanyId = null)
    {
        var companyId = GetCompanyId();
        if (selectedCompanyId.HasValue)
        {
            if (!User.IsInRole(AppRoles.SuperAdmin))
                return Forbid();

            var companies = await _companies.GetCompaniesAsync(cancellationToken);
            if (!companies.Any(x => x.Id == selectedCompanyId.Value))
                return NotFound(new { message = "Selected company was not found." });

            companyId = selectedCompanyId;
        }
        if (companyId is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "A company must be assigned or selected to view this dashboard."
            });
        }

        var dashboard = await _dashboardService.GetAsync(
            companyId.Value,
            cancellationToken);

        return Ok(dashboard);
    }

    private Guid? GetCompanyId()
    {
        var value =
            User.FindFirst("companyId")?.Value ??
            User.FindFirst("CompanyId")?.Value ??
            User.FindFirst("company_id")?.Value;

        return Guid.TryParse(value, out var companyId) && companyId != Guid.Empty
            ? companyId
            : null;
    }
}
