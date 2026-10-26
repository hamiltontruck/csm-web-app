using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.HRM.Shifts;
using CSM.Application.HRM.Shifts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ShiftsController : ControllerBase
{
    private const string ReadRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.SiteManager + "," + AppRoles.Supervisor + "," + AppRoles.HrOfficer + "," + AppRoles.HROfficer;
    private const string WriteRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.HrOfficer + "," + AppRoles.HROfficer;

    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet]
    [Authorize(Roles = ReadRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpGet("{shiftId:guid}")]
    [Authorize(Roles = ReadRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = WriteRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpPut("{shiftId:guid}")]
    [Authorize(Roles = WriteRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpPut("{shiftId:guid}/active-status")]
    [Authorize(Roles = WriteRoles)]
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
            return ErrorResult(ex);
        }
    }

    private ObjectResult ErrorResult(ShiftManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to access shifts." or
            "You are not authorized to create or modify shifts." or
            "Current user is not assigned to a company." or
            "You cannot access shifts outside your company." or
            "Current user was not found or is inactive." or
            "Authenticated user identifier is invalid." => StatusCode(403, body),
            "Shift was not found." => NotFound(body),
            _ => BadRequest(body)
        };
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