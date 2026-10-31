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

    private async Task<Guid> GetCompanyId(CancellationToken ct)
    {
        return await _db.Users
            .Where(x => x.Id == UserId)
            .Select(x => x.CompanyId!.Value)
            .FirstAsync(ct);
    }

    [HttpGet]
    public async Task<ActionResult<List<QualityChecklistResponse>>> GetAll(
        CancellationToken ct)
    {
        var companyId = await GetCompanyId(ct);
        return Ok(await _service.GetAllAsync(companyId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QualityChecklistResponse>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var companyId = await GetCompanyId(ct);

        var result = await _service.GetByIdAsync(companyId, id, ct);

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
        Console.WriteLine("========== QC UPDATE ==========");
        Console.WriteLine($"Checklist Id       : {id}");
        Console.WriteLine($"ConstructionSiteId : {request.ConstructionSiteId}");
        Console.WriteLine($"Name               : {request.Name}");

        var companyId = await GetCompanyId(ct);

        var result = await _service.UpdateAsync(
            companyId,
            id,
            request,
            ct);

        return Ok(result);
    }
}