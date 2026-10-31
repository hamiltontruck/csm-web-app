using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Projects.DailyProgress;
using CSM.Application.Projects.DailyProgress.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[ScopedProjectError]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager + "," + AppRoles.Supervisor)]
[Authorize(Policy = "Projects.DailyProgress.View")]
[Authorize(Policy = "Projects.View")]
public sealed class DailyProgressLogsController : ControllerBase
{
    private readonly IDailyProgressLogService _dailyProgressLogService;

    public DailyProgressLogsController(
        IDailyProgressLogService dailyProgressLogService)
    {
        _dailyProgressLogService = dailyProgressLogService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<DailyProgressLogResponse>>> GetAll(
        [FromQuery] Guid projectId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var logs = await _dailyProgressLogService.GetAllAsync(
                GetCurrentUserId(),
                projectId,
                fromDate,
                toDate,
                cancellationToken);

            return Ok(logs);
        }
        catch (DailyProgressLogManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{logId:guid}")]
    public async Task<ActionResult<DailyProgressLogResponse>> GetById(
        Guid logId,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _dailyProgressLogService.GetByIdAsync(
                GetCurrentUserId(),
                logId,
                cancellationToken);

            return Ok(log);
        }
        catch (DailyProgressLogManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Projects.DailyProgress.Create")]
    public async Task<ActionResult<DailyProgressLogResponse>> Create(
        CreateDailyProgressLogRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _dailyProgressLogService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { logId = log.Id },
                log);
        }
        catch (DailyProgressLogManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{logId:guid}")]
    [Authorize(Policy = "Projects.DailyProgress.Create")]
    public async Task<ActionResult<DailyProgressLogResponse>> Update(
        Guid logId,
        UpdateDailyProgressLogRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _dailyProgressLogService.UpdateAsync(
                GetCurrentUserId(),
                logId,
                request,
                cancellationToken);

            return Ok(log);
        }
        catch (DailyProgressLogManagementException ex)
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
            throw new DailyProgressLogManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}