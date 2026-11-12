using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/role-options")]
public sealed class UserRoleOptionsController(IUserRoleOptionsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<string>>> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        try { return Ok(await service.GetAssignableRolesAsync(actorId, cancellationToken)); }
        catch (UserManagementException) { return Forbid(); }
    }
}
