using System.Security.Claims;
using CSM.Application.Features.Compliance.QualityChecklists;
using CSM.Infrastructure.Features.Compliance.QualityChecklists;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CSM.Api.Controllers;

[Authorize]
[ApiController]
[ComplianceAccessExceptionFilter]
[Route("api/[controller]")]
[Authorize(Policy = "Compliance.View")]
public class QualityChecklistController : ControllerBase
{
    private readonly QualityChecklistService _service;
    private readonly ApplicationDbContext _db;

    public QualityChecklistController(
        QualityChecklistService service,
        ApplicationDbContext db)
    {
        _service = service;
        _db = db;
    }

    private Guid UserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<QualityChecklistResponse>>> GetAll(
        CancellationToken ct)
    {
        return Ok(await _service.GetAllAsync(UserId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QualityChecklistResponse>> GetById(
        Guid id,
        CancellationToken ct)
    {

        var result = await _service.GetByIdAsync(UserId, id, ct);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<QualityChecklistResponse>> Create(
        CreateQualityChecklistRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(UserId, request, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<QualityChecklistResponse>> Update(
        Guid id,
        UpdateQualityChecklistRequest request,
        CancellationToken ct)
    {


        var result = await _service.UpdateAsync(
            UserId,
            id,
            request,
            ct);

        return Ok(result);
    }
}