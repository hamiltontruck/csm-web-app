using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.HRM.Attendances;
using CSM.Application.HRM.Attendances.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AttendancesController : ControllerBase
{
    private const string RecordRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.SiteManager + "," + AppRoles.Supervisor + "," + AppRoles.HrOfficer + "," + AppRoles.HROfficer;
    private const string ReviewRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager;

    private readonly IAttendanceService _attendanceService;

    public AttendancesController(
        IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet]
    [Authorize(Roles = RecordRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpGet("{attendanceId:guid}")]
    [Authorize(Roles = RecordRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpPost("check-in")]
    [Authorize(Roles = RecordRoles)]
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
            return ErrorResult(ex);
        }
    }

    [HttpPut("{attendanceId:guid}/check-out")]
    [Authorize(Roles = RecordRoles)]
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
            return ErrorResult(ex);
        }
    }

    private ActionResult ErrorResult(AttendanceManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to access attendance." or
            "You are not authorized to record attendance." or
            "You are not authorized to approve attendance." or
            "You are not authorized to override attendance." or
            "Current user is not assigned to a company." or
            "Current user was not found or is inactive." or
            "Authenticated user identifier is invalid." or
            "You cannot access attendance outside your company." or
            "Employee does not belong to your company." or
            "Construction site does not belong to your company." or
            "Shift does not belong to your company." => StatusCode(403, body),
            "Attendance was not found." or
            "Employee was not found." or
            "Construction site was not found." or
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
            throw new AttendanceManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }

    [HttpPut("{attendanceId:guid}/approve")]
    [Authorize(Roles = ReviewRoles)]
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
            return ErrorResult(ex);
        }
    }
    [HttpPut("{attendanceId:guid}/override")]
    [Authorize(Roles = ReviewRoles)]
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
            return ErrorResult(ex);
        }
    }
}