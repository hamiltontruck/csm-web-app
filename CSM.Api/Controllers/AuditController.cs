using CSM.Application.Common.Audit;
using CSM.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.UserManagement)]
[Authorize(Policy = "Users.View")]
public sealed class AuditController : CurrentUserControllerBase
{
    private readonly IAuditQueryService _service;

    public AuditController(IAuditQueryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AuditLogDto>>> GetAll(
        [FromQuery] string? module,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetLogsAsync(CurrentUserId, module, fromUtc, toUtc, cancellationToken));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
