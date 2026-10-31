using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Procurement.EquipmentAssignments;
using CSM.Application.Procurement.EquipmentAssignments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
[Authorize(Policy = "Procurement.Inventory.View")]
public sealed class EquipmentAssignmentsController : ControllerBase
{
    private readonly IEquipmentAssignmentService
        _equipmentAssignmentService;

    public EquipmentAssignmentsController(
        IEquipmentAssignmentService equipmentAssignmentService)
    {
        _equipmentAssignmentService =
            equipmentAssignmentService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<EquipmentAssignmentResponse>>> GetAll(
        [FromQuery] Guid? equipmentId,
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] Guid? projectId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignments =
                await _equipmentAssignmentService.GetAllAsync(
                    GetCurrentUserId(),
                    equipmentId,
                    constructionSiteId,
                    projectId,
                    activeOnly,
                    cancellationToken);

            return Ok(assignments);
        }
        catch (EquipmentAssignmentManagementException ex)
        {
            return AssignmentError(ex);
        }
    }

    [HttpGet("{assignmentId:guid}")]
    public async Task<ActionResult<EquipmentAssignmentResponse>> GetById(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _equipmentAssignmentService.GetByIdAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (EquipmentAssignmentManagementException ex)
        {
            return AssignmentError(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentAssignmentResponse>> Create(
        [FromBody] CreateEquipmentAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _equipmentAssignmentService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    assignmentId = assignment.Id
                },
                assignment);
        }
        catch (EquipmentAssignmentManagementException ex)
        {
            return AssignmentError(ex);
        }
    }

    [HttpPut("{assignmentId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentAssignmentResponse>> Update(
        Guid assignmentId,
        [FromBody] UpdateEquipmentAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _equipmentAssignmentService.UpdateAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    request,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (EquipmentAssignmentManagementException ex)
        {
            return AssignmentError(ex);
        }
    }

    [HttpPut("{assignmentId:guid}/release")]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentAssignmentResponse>> Release(
        Guid assignmentId,
        [FromBody] ReleaseEquipmentAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _equipmentAssignmentService.ReleaseAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    request.ReleasedAtUtc,
                    request.MeterReadingAtRelease,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (EquipmentAssignmentManagementException ex)
        {
            return AssignmentError(ex);
        }
    }

    private ObjectResult AssignmentError(EquipmentAssignmentManagementException exception)
    {
        var forbidden = exception.Message is
            "You are not authorized to access equipment assignments."
            or "Current user is not assigned to a company."
            or "You cannot access records outside your company.";
        return forbidden
            ? StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message })
            : BadRequest(new { message = exception.Message });
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new EquipmentAssignmentManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}

public sealed record ReleaseEquipmentAssignmentRequest(
    DateTime ReleasedAtUtc,
    decimal? MeterReadingAtRelease);
