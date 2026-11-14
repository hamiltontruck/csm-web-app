using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentHourLogs;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Equipment.Inventory.View")]
public class EquipmentHourLogsController : ControllerBase
{
    private readonly EquipmentHourLogService _service;
    public EquipmentHourLogsController(EquipmentHourLogService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentHourLogResponse>>> GetAll(
        [FromQuery] Guid? equipmentId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetForUserAsync(GetCurrentUserId(), equipmentId, cancellationToken));
        }
        catch (EquipmentManagementException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Equipment.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentHourLogResponse>> Create(
        CreateEquipmentHourLogRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.CreateForUserAsync(GetCurrentUserId(), request, cancellationToken));
        }
        catch (EquipmentManagementException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (EquipmentHourLogValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(value, out var userId))
            throw new EquipmentManagementException("Current user identity is invalid.");
        return userId;
    }
}
