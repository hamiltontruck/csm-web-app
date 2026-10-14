using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Projects;
using CSM.Application.Projects.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CSM.Application.Projects.Progress;
using CSM.Application.Projects.Progress.Dtos;
using CSM.Application.Projects.Tasks;
using CSM.Application.Projects.Tasks.Dtos;


namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectProgressService _progressService;
    private readonly IWorkTaskService _workTaskService;

    public ProjectsController(
        IProjectService projectService,
        IProjectProgressService progressService,
        IWorkTaskService workTaskService)
    {
        _projectService = projectService;
        _progressService = progressService;
        _workTaskService = workTaskService;
    }

    [HttpGet]
    [Authorize(Policy = "Projects.View")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectResponse>>> GetAll(
        [FromQuery] Guid? constructionSiteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var projects = await _projectService.GetAllAsync(
                GetCurrentUserId(),
                constructionSiteId,
                cancellationToken);

            return Ok(projects);
        }
        catch (ProjectManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{projectId:guid}")]
    [Authorize(Policy = "Projects.View")]
    public async Task<ActionResult<ProjectResponse>> GetById(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.GetByIdAsync(
                GetCurrentUserId(),
                projectId,
                cancellationToken);

            return Ok(project);
        }
        catch (ProjectManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Projects.Manage")]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { projectId = project.Id },
                project);
        }
        catch (ProjectManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPut("{projectId:guid}")]
    [Authorize(Policy = "Projects.Manage")]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.UpdateAsync(
                GetCurrentUserId(),
                projectId,
                request,
                cancellationToken);

            return Ok(project);
        }
        catch (ProjectManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPut("{projectId:guid}/status")]
    [Authorize(Policy = "Projects.Manage")]
    public async Task<ActionResult<ProjectResponse>> ChangeStatus(
        Guid projectId,
        ChangeProjectStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectService.ChangeStatusAsync(
                GetCurrentUserId(),
                projectId,
                request,
                cancellationToken);

            return Ok(project);
        }
        catch (ProjectManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new ProjectManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }

    [HttpGet("{projectId:guid}/progress")]
    [Authorize(Policy = "Projects.View")]
    public async Task<ActionResult<ProjectProgressDashboardResponse>> GetProjectProgress(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _progressService.GetDashboardAsync(
                GetCurrentUserId(),
                projectId,
                cancellationToken);

            return Ok(result);
        }
        catch (ProjectManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpGet("{projectId:guid}/tasks")]
    [Authorize(Policy = "Projects.View")]
    public async Task<ActionResult<IReadOnlyCollection<WorkTaskResponse>>> GetWorkTasks(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = GetCurrentUserId();

            var tasks = await _workTaskService.GetAllAsync(
                currentUserId,
                projectId,
                cancellationToken: cancellationToken);

            return Ok(tasks);
        }
        catch (WorkTaskManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPost("{projectId:guid}/tasks")]
    [Authorize(Policy = "Projects.Manage")]
    public async Task<ActionResult<WorkTaskResponse>> CreateWorkTask(
        Guid projectId,
        CreateWorkTaskRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = GetCurrentUserId();

            var createRequest = new CreateWorkTaskRequest(
                projectId,
                request.ProjectPhaseId,
                request.MilestoneId,
                request.TaskNumber,
                request.Title,
                request.Description,
                request.PlannedStartDate,
                request.PlannedEndDate,
                request.Priority);

            var task = await _workTaskService.CreateAsync(
                currentUserId,
                createRequest,
                cancellationToken);

            return CreatedAtAction(nameof(GetWorkTasks), new { projectId }, task);
        }
        catch (WorkTaskManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }


}