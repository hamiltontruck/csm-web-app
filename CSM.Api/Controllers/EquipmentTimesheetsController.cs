using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentTimesheets;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Equipment.Inventory.View")]
public sealed class EquipmentTimesheetsController : ControllerBase
{
    private readonly EquipmentTimesheetService _service;
    public EquipmentTimesheetsController(EquipmentTimesheetService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentTimesheetResponse>>> GetAll(
        [FromQuery] Guid? equipmentId, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetForUserAsync(GetCurrentUserId(), equipmentId, cancellationToken)); }
        catch (EquipmentManagementException ex) { return ErrorResult(ex); }
    }

    [HttpPost]
    [Authorize(Policy = "Equipment.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentTimesheetResponse>> Create(
        CreateEquipmentTimesheetRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.CreateForUserAsync(GetCurrentUserId(), request, cancellationToken)); }
        catch (EquipmentManagementException ex) { return ErrorResult(ex); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentTimesheetResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.GetByIdForUserAsync(GetCurrentUserId(), id, cancellationToken)); }
        catch (EquipmentManagementException ex) { return ErrorResult(ex); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Equipment.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentTimesheetResponse>> Update(
        Guid id, CreateEquipmentTimesheetRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _service.UpdateForUserAsync(GetCurrentUserId(), id, request, cancellationToken)); }
        catch (EquipmentManagementException ex) { return ErrorResult(ex); }
    }

    private ActionResult ErrorResult(EquipmentManagementException ex) => ex.Message switch
    {
        "Timesheet not found." => NotFound(new { message = ex.Message }),
        "Current user identity is invalid." or "Current user was not found or is inactive." or
        "Current user is not assigned to a company." or "You do not have permission to access equipment timesheets." =>
            StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }),
        _ => BadRequest(new { message = ex.Message })
    };

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(value, out var userId))
            throw new EquipmentManagementException("Current user identity is invalid.");
        return userId;
    }
}
