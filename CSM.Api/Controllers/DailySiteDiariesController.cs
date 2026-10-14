using CSM.Application.Projects.DailySiteDiaries;
using CSM.Application.Projects.DailySiteDiaries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/daily-site-diaries")]
[Authorize]
public class DailySiteDiaryController : ControllerBase
{
    private readonly IDailySiteDiaryService _service;

    public DailySiteDiaryController(IDailySiteDiaryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DailySiteDiaryResponse>>> GetAll(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? siteId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(
            Guid.Empty,
            projectId,
            siteId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DailySiteDiaryResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            Guid.Empty,
            id,
            cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DailySiteDiaryResponse>> Create(
        CreateDailySiteDiaryRequest request,
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
    public async Task<ActionResult<DailySiteDiaryResponse>> Update(
        Guid id,
        UpdateDailySiteDiaryRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(
            Guid.Empty,
            id,
            request,
            cancellationToken);

        if (updated is null)
            return NotFound();

        return Ok(updated);
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

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}