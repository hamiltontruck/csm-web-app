using CSM.Application.Common.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuditController : ControllerBase
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
        var logs = await _service.GetLogsAsync(
            module,
            fromUtc,
            toUtc,
            cancellationToken);

        return Ok(logs);
    }
}