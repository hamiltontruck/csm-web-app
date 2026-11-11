using System.Security.Claims;
using CSM.Application.AccountSettings;
using CSM.Application.Common.Exceptions;
using CSM.Application.Users.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController, Authorize, Route("api/auth")]
public sealed class AccountSettingsController(IAccountSettingsService settings) : ControllerBase
{
    private string? CurrentToken => Request.Headers["X-Current-Refresh-Token"].FirstOrDefault();
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private async Task<IActionResult> Own(Func<Guid, Task<object?>> action)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try { return Ok(await action(userId)); }
        catch (AuthenticationException) { return Unauthorized(new { message = "The account or current session is unavailable. Sign in again." }); }
        catch (UserManagementException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(new { message = "The requested session is unavailable." }); }
    }
    [HttpGet("me/profile")]
    public Task<IActionResult> Profile(CancellationToken ct) => Own(async id => await settings.GetProfileAsync(id, ct));
    [HttpPut("me/profile")]
    public Task<IActionResult> UpdateProfile(UpdateMyProfileRequest request, CancellationToken ct) => Own(async id => await settings.UpdateProfileAsync(id, request, ct));
    [HttpGet("organization-membership")]
    public Task<IActionResult> Memberships(CancellationToken ct) => Own(async id => await settings.GetMembershipsAsync(id, ct));
    [HttpGet("login-history")]
    public Task<IActionResult> History(CancellationToken ct) => Own(async id => await settings.GetLoginHistoryAsync(id, ct));
    [HttpGet("sessions")]
    public Task<IActionResult> Sessions(CancellationToken ct) => Own(async id => await settings.GetSessionsAsync(id, CurrentToken, ct));
    [HttpDelete("sessions/{sessionId:guid}")]
    public Task<IActionResult> Revoke(Guid sessionId, CancellationToken ct) => Own(async id => { await settings.RevokeSessionAsync(id, sessionId, CurrentToken, Ip, ct); return new { message = "Session renewal revoked." }; });
    [HttpPost("sessions/revoke-others")]
    public Task<IActionResult> RevokeOthers(CancellationToken ct) => Own(async id => new { revokedCount = await settings.RevokeOtherSessionsAsync(id, CurrentToken, Ip, ct) });
}
