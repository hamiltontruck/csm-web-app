using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Users;
using CSM.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Policy = "Users.View")]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var users = await _userService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(users);
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.GetByIdAsync(
                GetCurrentUserId(),
                userId,
                cancellationToken);

            return Ok(user);
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Users.Create")]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { userId = user.Id },
                user);
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{userId:guid}")]
    [Authorize(Policy = "Users.Edit")]
    public async Task<ActionResult<UserResponse>> Update(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.UpdateAsync(
                GetCurrentUserId(),
                userId,
                request,
                cancellationToken);

            return Ok(user);
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> SetRoles(
        Guid userId,
        SetUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _userService.SetRolesAsync(
                GetCurrentUserId(),
                userId,
                request,
                cancellationToken);

            return NoContent();
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{userId:guid}/sites")]
    public async Task<IActionResult> SetSites(
        Guid userId,
        SetUserSitesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _userService.SetSitesAsync(
                GetCurrentUserId(),
                userId,
                request,
                cancellationToken);

            return NoContent();
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{userId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _userService.SetActiveStatusAsync(
                GetCurrentUserId(),
                userId,
                true,
                cancellationToken);

            return NoContent();
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{userId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _userService.SetActiveStatusAsync(
                GetCurrentUserId(),
                userId,
                false,
                cancellationToken);

            return NoContent();
        }
        catch (UserManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = "Users.Delete")]
    public async Task<IActionResult> Delete(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        await _userService.DeleteAsync(
            currentUserId,
            userId,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("{userId:guid}/profile")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        return Ok(await _userService.GetProfileAsync(
            currentUserId,
            userId,
            cancellationToken));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyCollection<UserAuditResponse>>> GetAudit(
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        return Ok(await _userService.GetAuditAsync(
            currentUserId,
            cancellationToken));
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UserManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}