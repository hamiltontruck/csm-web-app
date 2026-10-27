using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.HRM.Timesheets;
using CSM.Application.HRM.Timesheets.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TimesheetsController : ControllerBase
{
    private const string ApproveRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager;
    private const string ManageRoles = ApproveRoles + "," + AppRoles.HrOfficer + "," + AppRoles.HROfficer;
    private const string ReadRoles = ManageRoles + "," + AppRoles.Supervisor;

    private readonly ITimesheetService _timesheetService;

    public TimesheetsController(
        ITimesheetService timesheetService)
    {
        _timesheetService = timesheetService;
    }

    [HttpGet]
    [Authorize(Roles = ReadRoles)]
    public async Task<
        ActionResult<IReadOnlyCollection<TimesheetResponse>>> GetAll(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timesheets =
                await _timesheetService.GetAllAsync(
                    GetCurrentUserId(),
                    employeeId,
                    fromDate,
                    toDate,
                    cancellationToken);

            return Ok(timesheets);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpGet("{timesheetId:guid}")]
    [Authorize(Roles = ReadRoles)]
    public async Task<ActionResult<TimesheetResponse>> GetById(
        Guid timesheetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var timesheet =
                await _timesheetService.GetByIdAsync(
                    GetCurrentUserId(),
                    timesheetId,
                    cancellationToken);

            return Ok(timesheet);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<TimesheetResponse>> Create(
        CreateTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timesheet =
                await _timesheetService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { timesheetId = timesheet.Id },
                timesheet);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{timesheetId:guid}/submit")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<TimesheetResponse>> Submit(
        Guid timesheetId,
        SubmitTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timesheet =
                await _timesheetService.SubmitAsync(
                    GetCurrentUserId(),
                    timesheetId,
                    request,
                    cancellationToken);

            return Ok(timesheet);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{timesheetId:guid}/review")]
    [Authorize(Roles = ApproveRoles)]
    public async Task<ActionResult<TimesheetResponse>> Review(
        Guid timesheetId,
        ReviewTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timesheet =
                await _timesheetService.ReviewAsync(
                    GetCurrentUserId(),
                    timesheetId,
                    request,
                    cancellationToken);

            return Ok(timesheet);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{timesheetId:guid}/cancel")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<TimesheetResponse>> Cancel(
        Guid timesheetId,
        CancelTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var timesheet =
                await _timesheetService.CancelAsync(
                    GetCurrentUserId(),
                    timesheetId,
                    request,
                    cancellationToken);

            return Ok(timesheet);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPost("generate")]
    [Authorize(Roles = ManageRoles)]
    public async Task<ActionResult<TimesheetResponse>> Generate(
        GenerateTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _timesheetService.GenerateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (TimesheetManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    private ActionResult ErrorResult(TimesheetManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to access timesheets." or
            "You are not authorized to manage timesheets." or
            "You are not authorized to approve timesheets." or
            "You cannot access timesheets outside your company." or
            "Current user is not assigned to a company." or
            "Current user was not found." or
            "Current user account is inactive." or
            "Authenticated user identifier is missing." => StatusCode(403, body),
            "Timesheet was not found." => NotFound(body),
            _ => BadRequest(body)
        };
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new TimesheetManagementException(
                "Authenticated user identifier is missing.");
        }

        return userId;
    }
}
