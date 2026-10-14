using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Sites;
using CSM.Application.Sites.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SitesController : ControllerBase
{
    private readonly ISiteService _siteService;

    public SitesController(ISiteService siteService)
    {
        _siteService = siteService;
    }

    [HttpGet]
    [Authorize(Policy = "Sites.View")]
    public async Task<ActionResult<IReadOnlyCollection<SiteResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var sites = await _siteService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(sites);
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{siteId:guid}")]
    [Authorize(Policy = "Sites.View")]
    public async Task<ActionResult<SiteResponse>> GetById(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await _siteService.GetByIdAsync(
                GetCurrentUserId(),
                siteId,
                cancellationToken);

            return Ok(site);
        }
        catch (SiteManagementException ex)
{
    return StatusCode(
        StatusCodes.Status403Forbidden,
        new { message = ex.Message });
}
    }

    [HttpPost]
    [Authorize(Policy = "Sites.Manage")]
    public async Task<ActionResult<SiteResponse>> Create(
        CreateSiteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await _siteService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { siteId = site.Id },
                site);
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{siteId:guid}")]
    [Authorize(Policy = "Sites.Manage")]
    public async Task<ActionResult<SiteResponse>> Update(
        Guid siteId,
        UpdateSiteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await _siteService.UpdateAsync(
                GetCurrentUserId(),
                siteId,
                request,
                cancellationToken);

            return Ok(site);
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{siteId:guid}/status")]
    [Authorize(Policy = "Sites.Manage")]
    public async Task<ActionResult<SiteResponse>> ChangeStatus(
        Guid siteId,
        ChangeSiteStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await _siteService.ChangeStatusAsync(
                GetCurrentUserId(),
                siteId,
                request,
                cancellationToken);

            return Ok(site);
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{siteId:guid}/activate")]
    [Authorize(Policy = "Sites.Manage")]
    public async Task<IActionResult> Activate(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _siteService.SetActiveStatusAsync(
                GetCurrentUserId(),
                siteId,
                true,
                cancellationToken);

            return NoContent();
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{siteId:guid}/deactivate")]
    [Authorize(Policy = "Sites.Manage")]
    public async Task<IActionResult> Deactivate(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _siteService.SetActiveStatusAsync(
                GetCurrentUserId(),
                siteId,
                false,
                cancellationToken);

            return NoContent();
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("assignment-options")]
    [Authorize(Policy = "Sites.View")]
    public async Task<ActionResult<IReadOnlyCollection<SiteResponse>>> GetAssignmentOptions(
        CancellationToken cancellationToken)
    {
        try
        {
            var sites = await _siteService.GetAssignmentOptionsAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(sites);
        }
        catch (SiteManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }





    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new SiteManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}