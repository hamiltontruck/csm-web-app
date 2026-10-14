using CSM.Application.Projects.ProjectMilestones;
using CSM.Application.Projects.ProjectMilestones.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace CSM.Api.Controllers;

[ApiController]
[Route("api/projectmilestones")]
[Authorize]
public class ProjectMilestonesController : ControllerBase
{
    private readonly IProjectMilestoneService _service;

    public ProjectMilestonesController(
        IProjectMilestoneService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMilestoneResponse>>> GetAll(
        [FromQuery] Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(
            GetCurrentUserId(),
            projectId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectMilestoneResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            Guid.Empty,
            id,
            cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectMilestoneResponse>> Create(
        CreateProjectMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(
            Guid.Empty,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new Exception("Invalid authenticated user.");

        return userId;
    }
}