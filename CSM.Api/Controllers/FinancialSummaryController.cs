using CSM.Application.Finance.Summaries;
using CSM.Application.Finance.Summaries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CSM.API.Controllers;

[ApiController]
[Route("api/FinancialSummary")]
[Authorize]
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
        var currentUserId = GetCurrentUserId();

        var result = await _service.GetCompanySummaryAsync(
            currentUserId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("site/{constructionSiteId:guid}")]
    public async Task<ActionResult<SiteFinancialSummaryResponse>> GetSiteSummary(
        Guid constructionSiteId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _service.GetSiteSummaryAsync(
            currentUserId,
            constructionSiteId,
            cancellationToken);

        return Ok(result);
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