using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.SafetyIncidents;
using CSM.Application.Compliance.SafetyIncidents.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
public sealed class SafetyIncidentsController : ControllerBase
{
    private readonly ISafetyIncidentService _safetyIncidentService;

    public SafetyIncidentsController(
        ISafetyIncidentService safetyIncidentService)
    {
        _safetyIncidentService = safetyIncidentService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<SafetyIncidentResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _safetyIncidentService.GetAllAsync(
                    GetCurrentUserId(),
                    cancellationToken);

            return Ok(result);
        }
        catch (SafetyIncidentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{safetyIncidentId:guid}")]
    public async Task<ActionResult<SafetyIncidentResponse>> GetById(
        Guid safetyIncidentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _safetyIncidentService.GetByIdAsync(
                    GetCurrentUserId(),
                    safetyIncidentId,
                    cancellationToken);

            return Ok(result);
        }
        catch (SafetyIncidentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<SafetyIncidentResponse>> Create(
        [FromBody] CreateSafetyIncidentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _safetyIncidentService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { safetyIncidentId = result.Id },
                result);
        }
        catch (SafetyIncidentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{safetyIncidentId:guid}")]
    public async Task<ActionResult<SafetyIncidentResponse>> Update(
        Guid safetyIncidentId,
        [FromBody] UpdateSafetyIncidentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _safetyIncidentService.UpdateAsync(
                    GetCurrentUserId(),
                    safetyIncidentId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (SafetyIncidentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                userId,
                out var parsedUserId))
        {
            throw new SafetyIncidentManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
