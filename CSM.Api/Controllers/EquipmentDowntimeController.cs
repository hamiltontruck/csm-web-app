using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Procurement.EquipmentDowntime;
using CSM.Application.Procurement.EquipmentDowntime.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
public sealed class EquipmentDowntimeController : ControllerBase
{
    private readonly IEquipmentDowntimeService _service;

    public EquipmentDowntimeController(
        IEquipmentDowntimeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EquipmentDowntimeResponse>>> GetAll(
        [FromQuery] Guid? equipmentId = null,
        [FromQuery] Guid? constructionSiteId = null,
        [FromQuery] bool? openOnly = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetAllAsync(
                GetCurrentUserId(),
                equipmentId,
                constructionSiteId,
                openOnly,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentDowntimeManagementException ex)
        {
            return DowntimeError(ex);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentDowntimeResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.GetByIdAsync(
                GetCurrentUserId(),
                id,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentDowntimeManagementException ex)
        {
            return DowntimeError(ex, notFound: true);
        }
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentDowntimeResponse>> Create(
        [FromBody] CreateEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }
        catch (EquipmentDowntimeManagementException ex)
        {
            return DowntimeError(ex);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EquipmentDowntimeResponse>> Update(
        Guid id,
        [FromBody] UpdateEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.UpdateAsync(
                GetCurrentUserId(),
                id,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentDowntimeManagementException ex)
        {
            return DowntimeError(ex);
        }
    }

    [HttpPut("{id:guid}/close")]
    public async Task<ActionResult<EquipmentDowntimeResponse>> Close(
        Guid id,
        [FromBody] CloseEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.CloseAsync(
                GetCurrentUserId(),
                id,
                request.EndedAtUtc,
                request.Resolution,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentDowntimeManagementException ex)
        {
            return DowntimeError(ex);
        }
    }

    private ObjectResult DowntimeError(EquipmentDowntimeManagementException exception, bool notFound = false)
    {
        var forbidden = exception.Message is
            "You are not authorized to manage equipment downtime."
            or "You are not authorized to access this equipment downtime record."
            or "Current user is not assigned to a company.";
        if (forbidden)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        return notFound
            ? NotFound(new { message = exception.Message })
            : BadRequest(new { message = exception.Message });
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new EquipmentDowntimeManagementException(
                "Authenticated user identifier is missing.");
        }

        return userId;
    }
}

public sealed record CloseEquipmentDowntimeRequest(
    DateTime EndedAtUtc,
    string? Resolution);
