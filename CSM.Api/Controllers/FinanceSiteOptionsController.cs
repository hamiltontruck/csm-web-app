using System.Security.Claims;
using CSM.Application.Finance.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/finance/sites/{siteId:guid}/options")]
public sealed class FinanceSiteOptionsController(IFinanceSiteOptionsService service) : ControllerBase
{
    [HttpGet("~/api/finance/action-capabilities")]
    public async Task<ActionResult<FinanceActionCapabilities>> GetActionCapabilities([FromQuery] string purpose, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        try { return Ok(await service.GetActionCapabilitiesAsync(actorId, purpose, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest(new { message = "Unknown finance action." }); }
    }

    [HttpGet]
    public async Task<ActionResult<FinanceSiteOptionsResponse>> Get(Guid siteId, [FromQuery] string purpose, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        try { return Ok(await service.GetAsync(actorId, siteId, purpose, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Active site was not found." }); }
        catch (ArgumentException) { return BadRequest(new { message = "Unknown finance form." }); }
    }
}
