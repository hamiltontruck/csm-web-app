using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Projects.Milestones;
using CSM.Application.Projects.Milestones.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProjectManagement)]
public sealed class MilestonesController : ControllerBase
{
    private readonly IMilestoneService _milestoneService;

    public MilestonesController(IMilestoneService milestoneService)
    {
        _milestoneService = milestoneService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MilestoneResponse>>> GetAll(
        [FromQuery] Guid projectId,
        [FromQuery] Guid? projectPhaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var milestones = await _milestoneService.GetAllAsync(
                GetCurrentUserId(),
                projectId,
                projectPhaseId,
                cancellationToken);

            return Ok(milestones);
        }
        catch (MilestoneManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{milestoneId:guid}")]
    public async Task<ActionResult<MilestoneResponse>> GetById(
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        try
        {
            var milestone = await _milestoneService.GetByIdAsync(
                GetCurrentUserId(),
                milestoneId,
                cancellationToken);

            return Ok(milestone);
        }
        catch (MilestoneManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<MilestoneResponse>> Create(
        CreateMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var milestone = await _milestoneService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { milestoneId = milestone.Id },
                milestone);
        }
        catch (MilestoneManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{milestoneId:guid}")]
    public async Task<ActionResult<MilestoneResponse>> Update(
        Guid milestoneId,
        UpdateMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var milestone = await _milestoneService.UpdateAsync(
                GetCurrentUserId(),
                milestoneId,
                request,
                cancellationToken);

            return Ok(milestone);
        }
        catch (MilestoneManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{milestoneId:guid}/complete")]
    public async Task<ActionResult<MilestoneResponse>> Complete(
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        try
        {
            var milestone = await _milestoneService.CompleteAsync(
                GetCurrentUserId(),
                milestoneId,
                cancellationToken);

            return Ok(milestone);
        }
        catch (MilestoneManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{milestoneId:guid}/reopen")]
    public async Task<ActionResult<MilestoneResponse>> Reopen(
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        try
        {
            var milestone = await _milestoneService.ReopenAsync(
                GetCurrentUserId(),
                milestoneId,
                cancellationToken);

            return Ok(milestone);
        }
        catch (MilestoneManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new MilestoneManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}