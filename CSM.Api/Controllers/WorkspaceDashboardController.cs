using System.Security.Claims;
using CSM.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/dashboard/workspace")]
[Authorize]
public sealed class WorkspaceDashboardController(IWorkspaceDashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken, [FromQuery] Guid? selectedCompanyId = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized(new { message = "Please sign in again." });
        try
        {
            return Ok(await service.GetAsync(actorId, selectedCompanyId, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "A company must be assigned or selected, and accessible to your account." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Selected company was not found." });
        }
    }
}
