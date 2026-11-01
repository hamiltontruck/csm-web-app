using CSM.Domain.Constants;
using CSM.Application.Common.Security;
using CSM.Application.Projects.Equipment;
using CSM.Application.Projects.Equipment.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin)]
[Authorize(Policy = "Projects.View")]
[ScopedProjectError]
public sealed class ProjectEquipmentController : CurrentUserControllerBase
{
    private readonly IProjectEquipmentService _service;

    public ProjectEquipmentController(
        IProjectEquipmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EquipmentResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(CurrentUserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(CurrentUserId, id, cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Projects.Manage")]
    public async Task<ActionResult<EquipmentResponse>> Create(
        [FromBody] CreateEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            CurrentUserId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Projects.Manage")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }
}