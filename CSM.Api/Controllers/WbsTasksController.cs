using System.Security.Claims;
using CSM.Domain.Constants;
using CSM.Api.Controllers;
using CSM.Application.Projects.WbsTasks;
using CSM.Application.Projects.WbsTasks.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/wbs-tasks")]
[Authorize(Policy = "Projects.View")]
[ScopedProjectError]
public class WbsTasksController : ControllerBase
{
    private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private readonly IWbsTaskService _service;

    public WbsTasksController(IWbsTaskService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WbsTaskResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(
            CurrentUserId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WbsTaskResponse>> GetById(
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
    public async Task<ActionResult<WbsTaskResponse>> Create(
        CreateWbsTaskRequest request,
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