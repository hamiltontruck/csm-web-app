using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Compliance.SafetyIncidents;
using CSM.Application.Compliance.SafetyIncidents.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
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
            return ErrorResult(ex);
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
            return ErrorResult(ex);
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
            return ErrorResult(ex);
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
            return ErrorResult(ex);
        }
    }

    private ActionResult ErrorResult(SafetyIncidentManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to manage safety incidents." or
            "You are not authorized to access this safety incident." or
            "Current user is not assigned to a company." or
            "Authenticated user was not found." or
            "Authenticated user identifier is invalid." => StatusCode(403, body),
            "Safety incident was not found." => NotFound(body),
            _ => BadRequest(body)
        };
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
