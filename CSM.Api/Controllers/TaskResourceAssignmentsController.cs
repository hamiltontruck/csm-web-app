using CSM.Application.Projects.ResourceAssignments;
using CSM.Application.Projects.ResourceAssignments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CSM.API.Controllers;

[ApiController]
[Authorize]
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
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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