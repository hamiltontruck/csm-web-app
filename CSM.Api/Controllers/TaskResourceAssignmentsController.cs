using CSM.Domain.Constants;
using CSM.Api.Controllers;
using CSM.Application.Projects.ResourceAssignments;
using CSM.Application.Projects.ResourceAssignments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CSM.API.Controllers;

[ApiController]
[Authorize(Policy = "Projects.View")]
[ScopedProjectError]
[Route("api/task-resource-assignments")]
public sealed class TaskResourceAssignmentsController : ControllerBase
{
    private readonly ITaskResourceAssignmentService _service;

    public TaskResourceAssignmentsController(
        ITaskResourceAssignmentService service)
    {
        _service = service;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet("task/{wbsTaskId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TaskResourceAssignmentResponse>>> GetByTask(
        Guid wbsTaskId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByTaskAsync(
            wbsTaskId,
            CurrentUserId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<ActionResult<TaskResourceAssignmentResponse>> Create(
        CreateTaskResourceAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            request,
            CurrentUserId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetByTask),
            new { wbsTaskId = result.WbsTaskId },
            result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<ActionResult<TaskResourceAssignmentResponse>> Update(
        Guid id,
        UpdateTaskResourceAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(
            id,
            request,
            CurrentUserId,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Projects.Manage")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(
            id,
            CurrentUserId,
            cancellationToken);

        return NoContent();
    }
}