using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.HRM.Leaves;
using CSM.Application.HRM.Leaves.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class LeavesController : ControllerBase
{
    private const string SubmitRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.SiteManager + "," + AppRoles.Supervisor + "," + AppRoles.HrOfficer + "," + AppRoles.HROfficer;
    private const string ReviewRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.SiteManager + "," + AppRoles.HrOfficer + "," + AppRoles.HROfficer;

    private readonly ILeaveService _leaveService;

    public LeavesController(ILeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    [HttpGet("employee-options")]
    [Authorize(Roles = SubmitRoles)]
    public async Task<ActionResult<IReadOnlyCollection<LeaveEmployeeOptionResponse>>> GetEmployeeOptions(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _leaveService.GetEmployeeOptionsAsync(GetCurrentUserId(), cancellationToken));
        }
        catch (LeaveManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpGet]
    [Authorize(Roles = SubmitRoles)]
    public async Task<
        ActionResult<IReadOnlyCollection<LeaveResponse>>> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaveRequests =
                await _leaveService.GetAllAsync(
                    GetCurrentUserId(),
                    employeeId,
                    fromDate,
                    toDate,
                    cancellationToken);

            return Ok(leaveRequests);
        }
        catch (LeaveManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpGet("{leaveRequestId:guid}")]
    [Authorize(Roles = SubmitRoles)]
    public async Task<ActionResult<LeaveResponse>> GetById(
        Guid leaveRequestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaveRequest =
                await _leaveService.GetByIdAsync(
                    GetCurrentUserId(),
                    leaveRequestId,
                    cancellationToken);

            return Ok(leaveRequest);
        }
        catch (LeaveManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = SubmitRoles)]
    public async Task<ActionResult<LeaveResponse>> Create(
        CreateLeaveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaveRequest =
                await _leaveService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    leaveRequestId = leaveRequest.Id
                },
                leaveRequest);
        }
        catch (LeaveManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{leaveRequestId:guid}/review")]
    [Authorize(Roles = ReviewRoles)]
    public async Task<ActionResult<LeaveResponse>> Review(
        Guid leaveRequestId,
        ReviewLeaveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaveRequest =
                await _leaveService.ReviewAsync(
                    GetCurrentUserId(),
                    leaveRequestId,
                    request,
                    cancellationToken);

            return Ok(leaveRequest);
        }
        catch (LeaveManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{leaveRequestId:guid}/cancel")]
    [Authorize(Roles = ReviewRoles)]
    public async Task<ActionResult<LeaveResponse>> Cancel(
        Guid leaveRequestId,
        CancelLeaveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var leaveRequest =
                await _leaveService.CancelAsync(
                    GetCurrentUserId(),
                    leaveRequestId,
                    request,
                    cancellationToken);

            return Ok(leaveRequest);
        }
        catch (LeaveManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    private ActionResult ErrorResult(LeaveManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to access leave requests." or
            "You are not authorized to create leave requests." or
            "You are not authorized to review leave requests." or
            "You are not authorized to cancel leave requests." or
            "You cannot access leave requests outside your company." or
            "Employee does not belong to your company." or
            "Current user is not assigned to a company." or
            "Current user was not found." or
            "Current user account is inactive." or
            "Authenticated user identifier is invalid." => StatusCode(403, body),
            "Leave request was not found." => NotFound(body),
            _ => BadRequest(body)
        };
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new LeaveManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
