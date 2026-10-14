using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.SiteAssignments;
using CSM.Application.HRM.SiteAssignments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class SiteAssignmentsController : ControllerBase
{
    private readonly ISiteAssignmentService _siteAssignmentService;

    public SiteAssignmentsController(
        ISiteAssignmentService siteAssignmentService)
    {
        _siteAssignmentService = siteAssignmentService;
    }

    [HttpGet]
    [Authorize(Policy = "HRM.SiteAssignment.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<SiteAssignmentResponse>>> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? constructionSiteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignments =
                await _siteAssignmentService.GetAllAsync(
                    GetCurrentUserId(),
                    employeeId,
                    constructionSiteId,
                    cancellationToken);

            return Ok(assignments);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{assignmentId:guid}")]
    [Authorize(Policy = "HRM.SiteAssignment.View")]
    public async Task<ActionResult<SiteAssignmentResponse>> GetById(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _siteAssignmentService.GetByIdAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "HRM.SiteAssignment.Manage")]
    public async Task<ActionResult<SiteAssignmentResponse>> Create(
        CreateSiteAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _siteAssignmentService.CreateAsync(
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
        catch (SiteAssignmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{assignmentId:guid}")]
    [Authorize(Policy = "HRM.SiteAssignment.Manage")]
    public async Task<ActionResult<SiteAssignmentResponse>> Update(
        Guid assignmentId,
        UpdateSiteAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _siteAssignmentService.UpdateAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    request,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new SiteAssignmentManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}