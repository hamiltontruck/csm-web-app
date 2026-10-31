using CSM.Application.Finance.Summaries;
using CSM.Application.Finance.Summaries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CSM.API.Controllers;

[ApiController]
[Route("api/FinancialSummary")]
[Authorize(Policy = "Reports.FinancialSummary.View")]
public sealed class FinancialSummaryController : ControllerBase
{
    private readonly IFinancialSummaryService _service;

    public FinancialSummaryController(
        IFinancialSummaryService service)
    {
        _service = service;
    }

    [HttpGet("company")]
    public async Task<ActionResult<CompanyFinancialSummaryResponse>> GetCompanySummary(
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetCompanySummaryAsync(GetCurrentUserId(), cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Financial summary was not found." }); }
    }

    [HttpGet("site/{constructionSiteId:guid}")]
    public async Task<ActionResult<SiteFinancialSummaryResponse>> GetSiteSummary(
        Guid constructionSiteId,
        CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetSiteSummaryAsync(GetCurrentUserId(), constructionSiteId, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Construction site was not found." }); }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user ID is missing.");
        }

        return userId;
    }
}