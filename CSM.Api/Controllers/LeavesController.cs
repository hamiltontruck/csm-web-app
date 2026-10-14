using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.Leaves;
using CSM.Application.HRM.Leaves.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class LeavesController : ControllerBase
{
    private readonly ILeaveService _leaveService;

    public LeavesController(ILeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    [HttpGet]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{leaveRequestId:guid}")]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{leaveRequestId:guid}/review")]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{leaveRequestId:guid}/cancel")]
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
            return BadRequest(new { message = ex.Message });
        }
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
