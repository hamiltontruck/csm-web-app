using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.Attendances;
using CSM.Application.HRM.Attendances.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class AttendancesController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendancesController(
        IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<AttendanceResponse>>> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var attendances =
                await _attendanceService.GetAllAsync(
                    GetCurrentUserId(),
                    employeeId,
                    constructionSiteId,
                    fromDate,
                    toDate,
                    cancellationToken);

            return Ok(attendances);
        }
        catch (AttendanceManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpGet("{attendanceId:guid}")]
    public async Task<ActionResult<AttendanceResponse>> GetById(
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var attendance =
                await _attendanceService.GetByIdAsync(
                    GetCurrentUserId(),
                    attendanceId,
                    cancellationToken);

            return Ok(attendance);
        }
        catch (AttendanceManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpPost("check-in")]
    public async Task<ActionResult<AttendanceResponse>> CheckIn(
        CheckInRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var attendance =
                await _attendanceService.CheckInAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    attendanceId = attendance.Id
                },
                attendance);
        }
        catch (AttendanceManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpPut("{attendanceId:guid}/check-out")]
    public async Task<ActionResult<AttendanceResponse>> CheckOut(
        Guid attendanceId,
        CheckOutRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var attendance =
                await _attendanceService.CheckOutAsync(
                    GetCurrentUserId(),
                    attendanceId,
                    request,
                    cancellationToken);

            return Ok(attendance);
        }
        catch (AttendanceManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new AttendanceManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }

    [HttpPut("{attendanceId:guid}/approve")]
    public async Task<ActionResult<AttendanceResponse>> Approve(
        Guid attendanceId,
        ApproveAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var attendance =
                await _attendanceService.ApproveAsync(
                    GetCurrentUserId(),
                    attendanceId,
                    request,
                    cancellationToken);

            return Ok(attendance);
        }
        catch (AttendanceManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }
    [HttpPut("{attendanceId:guid}/override")]
    public async Task<ActionResult<AttendanceResponse>> Override(
        Guid attendanceId,
        OverrideAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var attendance =
                await _attendanceService.OverrideAsync(
                    GetCurrentUserId(),
                    attendanceId,
                    request,
                    cancellationToken);

            return Ok(attendance);
        }
        catch (AttendanceManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }
}