using System.Security.Claims;
using CSM.Domain.Constants;
using CSM.Api.Controllers;
using CSM.Application.Projects.ProjectDelays;
using CSM.Application.Projects.ProjectDelays.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.API.Controllers;

[ApiController]
[Route("api/project-delays")]
[Authorize(Policy = "Projects.View")]
[ScopedProjectError]
public sealed class ProjectDelaysController : ControllerBase
{
    private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private readonly IProjectDelayService _service;

    public ProjectDelaysController(IProjectDelayService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(
            CurrentUserId,
            projectId,
            null,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            CurrentUserId,
            id,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<IActionResult> Create(
        CreateProjectDelayRequest request,
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

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProjectDelayRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(
            CurrentUserId,
            id,
            request,
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(
            CurrentUserId,
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}