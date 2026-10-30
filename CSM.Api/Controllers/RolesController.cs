using CSM.Application.Roles;
using CSM.Application.Roles.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = "Users.View")]
public class RolesController : CurrentUserControllerBase
{
    private readonly IRolePermissionService _service;

    public RolesController(IRolePermissionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<RoleSummaryDto>>> GetRoles(
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetRolesAsync(cancellationToken));
    }

    [HttpGet("{roleId:guid}/permissions")]
    public async Task<ActionResult<IReadOnlyCollection<RolePermissionDto>>> GetPermissions(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetPermissionsAsync(
            roleId,
            cancellationToken));
    }

    [HttpPut("{roleId:guid}/permissions")]
    [Authorize(Roles = CSM.Domain.Constants.AppRoles.SuperAdmin, Policy = "Users.Edit")]
    public async Task<IActionResult> UpdatePermissions(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.UpdatePermissionsAsync(CurrentUserId, roleId, request, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Role was not found." }); }
        catch (ArgumentException) { return BadRequest(new { message = "One or more permissions are invalid." }); }
    }
}
