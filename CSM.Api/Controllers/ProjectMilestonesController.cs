using CSM.Domain.Constants;
using CSM.Api.Controllers;
using CSM.Application.Projects.ProjectMilestones;
using CSM.Application.Projects.ProjectMilestones.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace CSM.Api.Controllers;

[ApiController]
[Route("api/projectmilestones")]
[Authorize(Policy = "Projects.View")]
[ScopedProjectError]
public class ProjectMilestonesController : ControllerBase
{
    private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

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
            CurrentUserId,
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
            CurrentUserId,
            id,
            cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<ActionResult<ProjectMilestoneResponse>> Create(
        CreateProjectMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(
            CurrentUserId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
    }

}