using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.EquipmentAssignments;
using CSM.Application.Procurement.EquipmentAssignments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
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
            return BadRequest(new { message = ex.Message });
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{assignmentId:guid}")]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{assignmentId:guid}/release")]
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
            return BadRequest(new { message = ex.Message });
        }
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
