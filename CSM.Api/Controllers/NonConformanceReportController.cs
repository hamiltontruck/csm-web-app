using System.Security.Claims;
using CSM.Application.Features.Compliance.NonConformanceReports;
using CSM.Infrastructure.Features.Compliance.NonConformanceReports;
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
public class NonConformanceReportController : ControllerBase
{
    private readonly NonConformanceReportService _service;
    private readonly ApplicationDbContext _db;

    public NonConformanceReportController(
        NonConformanceReportService service,
        ApplicationDbContext db)
    {
        _service = service;
        _db = db;
    }

    private Guid UserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<NonConformanceReportResponse>>> GetAll(
        CancellationToken ct)
    {
        return Ok(await _service.GetAllAsync(UserId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NonConformanceReportResponse>> GetById(
        Guid id,
        CancellationToken ct)
    {

        var item = await _service.GetByIdAsync(UserId, id, ct);

        if (item is null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<NonConformanceReportResponse>> Create(
        CreateNonConformanceReportRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(UserId, request, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<NonConformanceReportResponse>> Update(
        Guid id,
        UpdateNonConformanceReportRequest request,
        CancellationToken ct)
    {

        var result = await _service.UpdateAsync(UserId, id, request, ct);

        return Ok(result);
    }
}