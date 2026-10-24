using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.UserPreferences;
using CSM.Application.UserPreferences.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/user/preferences")]
[Authorize]
public sealed class UserPreferencesController : ControllerBase
{
private readonly IUserPreferencesService _preferencesService;

public UserPreferencesController(
    IUserPreferencesService preferencesService)
{
    _preferencesService = preferencesService;
}

[HttpGet]
public async Task<ActionResult<UserPreferencesResponse>> GetAsync(
    CancellationToken cancellationToken)
{
    if (!TryGetCurrentUserId(out var currentUserId))
    {
        return Unauthorized();
    }

    try
    {
        var preferences = await _preferencesService.GetAsync(
            currentUserId,
            cancellationToken);

        return Ok(preferences);
    }
    catch (UserManagementException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}

[HttpPut]
public async Task<ActionResult<UserPreferencesResponse>> UpdateAsync(
    [FromBody] UpdateUserPreferencesRequest request,
    CancellationToken cancellationToken)
{
    if (!TryGetCurrentUserId(out var currentUserId))
    {
        return Unauthorized();
    }

    try
    {
        var preferences = await _preferencesService.UpdateAsync(
            currentUserId,
            request,
            cancellationToken);

        return Ok(preferences);
    }
    catch (UserManagementException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}

private bool TryGetCurrentUserId(out Guid userId)
{
    var userIdClaim =
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    return Guid.TryParse(userIdClaim, out userId);
}
}