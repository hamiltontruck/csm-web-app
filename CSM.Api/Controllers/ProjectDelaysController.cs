using CSM.Application.Projects.ProjectDelays;
using CSM.Application.Projects.ProjectDelays.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.API.Controllers;

[ApiController]
[Route("api/project-delays")]
[Authorize]
public sealed class ProjectDelaysController : ControllerBase
{
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
            Guid.Empty,
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
            Guid.Empty,
            id,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateProjectDelayRequest request,
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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProjectDelayRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(
            Guid.Empty,
            id,
            request,
            cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(
            Guid.Empty,
            id,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}