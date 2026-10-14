using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.Shifts;
using CSM.Application.HRM.Shifts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<ShiftResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var shifts = await _shiftService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(shifts);
        }
        catch (ShiftManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{shiftId:guid}")]
    public async Task<ActionResult<ShiftResponse>> GetById(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        try
        {
            var shift = await _shiftService.GetByIdAsync(
                GetCurrentUserId(),
                shiftId,
                cancellationToken);

            return Ok(shift);
        }
        catch (ShiftManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ShiftResponse>> Create(
        CreateShiftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var shift = await _shiftService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { shiftId = shift.Id },
                shift);
        }
        catch (ShiftManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{shiftId:guid}")]
    public async Task<ActionResult<ShiftResponse>> Update(
        Guid shiftId,
        UpdateShiftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var shift = await _shiftService.UpdateAsync(
                GetCurrentUserId(),
                shiftId,
                request,
                cancellationToken);

            return Ok(shift);
        }
        catch (ShiftManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{shiftId:guid}/active-status")]
    public async Task<ActionResult<ShiftResponse>> ChangeActiveStatus(
        Guid shiftId,
        ChangeShiftActiveStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var shift = await _shiftService.ChangeActiveStatusAsync(
                GetCurrentUserId(),
                shiftId,
                request,
                cancellationToken);

            return Ok(shift);
        }
        catch (ShiftManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new ShiftManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}