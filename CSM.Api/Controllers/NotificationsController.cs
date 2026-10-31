using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Notifications;
using CSM.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Authorize(Policy = "Notifications.View")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(
        INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<NotificationResponse>>> GetAll(
        [FromQuery] bool? isRead,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _notificationService.GetAllAsync(
                    GetCurrentUserId(),
                    isRead,
                    cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (NotificationManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _notificationService.GetUnreadCountAsync(
                    GetCurrentUserId(),
                    cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (NotificationManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Notifications.Manage")]
    public async Task<ActionResult<NotificationResponse>> Create(
        [FromBody] CreateNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _notificationService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (NotificationManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<ActionResult<NotificationResponse>> MarkRead(
        Guid notificationId,
        [FromBody] MarkNotificationReadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _notificationService.MarkReadAsync(
                    GetCurrentUserId(),
                    notificationId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (NotificationManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new NotificationManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
