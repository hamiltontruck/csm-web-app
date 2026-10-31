using System.Security.Claims;
using CSM.Application.Features.Compliance.SafetyObservations.DTOs;
using CSM.Application.Features.Compliance.SafetyObservations.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Compliance.View")]
public class SafetyObservationController : ControllerBase
{
    private readonly ISafetyObservationService _service;

    public SafetyObservationController(
        ISafetyObservationService service)
    {
        _service = service;
    }

    private Guid CurrentUserId =>
        Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SafetyObservationListItem>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(
            await _service.GetAllAsync(
                CurrentUserId,
                cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyObservationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(
            await _service.GetByIdAsync(
                CurrentUserId,
                id,
                cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<SafetyObservationResponse>> Create(
        CreateSafetyObservationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            CurrentUserId,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<SafetyObservationResponse>> Update(
        Guid id,
        UpdateSafetyObservationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(
            await _service.UpdateAsync(
                CurrentUserId,
                id,
                request,
                cancellationToken));
    }
}