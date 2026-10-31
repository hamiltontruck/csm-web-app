using System.Security.Claims;
using CSM.Application.Projects.DailyProgressTaskEntries;
using CSM.Application.Projects.DailyProgressTaskEntries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[ScopedProjectError]
[Route("api/[controller]")]
[Authorize]
[Authorize(Policy = "Projects.DailyProgress.View")]
[Authorize(Policy = "Projects.View")]
public class DailyProgressTaskEntriesController : ControllerBase
{
    private readonly IDailyProgressTaskEntryService _service;

    public DailyProgressTaskEntriesController(
        IDailyProgressTaskEntryService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Policy = "Projects.DailyProgress.Create")]
    public async Task<IActionResult> Create(
        CreateDailyProgressTaskEntryRequest request)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        var result = await _service.CreateAsync(userId, request);

        return Ok(result);
    }

    [HttpGet("{logId:guid}")]
    public async Task<IActionResult> Get(Guid logId)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        var result = await _service.GetByLogAsync(userId, logId);

        return Ok(result);
    }

    [HttpPost("equipment")]
    [Authorize(Policy = "Projects.DailyProgress.Create")]
    public async Task<IActionResult> AddEquipment(
        CreateEquipmentEntryRequest request)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        return Ok(await _service.AddEquipmentAsync(userId, request));
    }

    [HttpGet("{taskEntryId:guid}/equipment")]
    public async Task<IActionResult> GetEquipment(Guid taskEntryId)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        return Ok(await _service.GetEquipmentAsync(userId, taskEntryId));
    }
}