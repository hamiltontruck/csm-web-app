using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Projects.Phases;
using CSM.Application.Projects.Phases.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProjectManagement)]
public sealed class ProjectPhasesController : ControllerBase
{
    private readonly IProjectPhaseService _phaseService;

    public ProjectPhasesController(
        IProjectPhaseService phaseService)
    {
        _phaseService = phaseService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<ProjectPhaseResponse>>> GetAll(
        [FromQuery] Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var phases = await _phaseService.GetAllAsync(
                GetCurrentUserId(),
                projectId,
                cancellationToken);

            return Ok(phases);
        }
        catch (ProjectPhaseManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{phaseId:guid}")]
    public async Task<ActionResult<ProjectPhaseResponse>> GetById(
        Guid phaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var phase = await _phaseService.GetByIdAsync(
                GetCurrentUserId(),
                phaseId,
                cancellationToken);

            return Ok(phase);
        }
        catch (ProjectPhaseManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProjectPhaseResponse>> Create(
        CreateProjectPhaseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var phase = await _phaseService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { phaseId = phase.Id },
                phase);
        }
        catch (ProjectPhaseManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{phaseId:guid}")]
    public async Task<ActionResult<ProjectPhaseResponse>> Update(
        Guid phaseId,
        UpdateProjectPhaseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var phase = await _phaseService.UpdateAsync(
                GetCurrentUserId(),
                phaseId,
                request,
                cancellationToken);

            return Ok(phase);
        }
        catch (ProjectPhaseManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{phaseId:guid}/status")]
    public async Task<ActionResult<ProjectPhaseResponse>> ChangeStatus(
        Guid phaseId,
        ChangeProjectPhaseStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var phase = await _phaseService.ChangeStatusAsync(
                GetCurrentUserId(),
                phaseId,
                request,
                cancellationToken);

            return Ok(phase);
        }
        catch (ProjectPhaseManagementException ex)
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
            throw new ProjectPhaseManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}