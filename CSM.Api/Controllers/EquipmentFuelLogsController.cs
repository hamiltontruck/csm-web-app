using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.EquipmentFuelLogs;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
[Authorize(Policy = "Procurement.Inventory.View")]
public sealed class EquipmentFuelLogsController : ControllerBase
{
    private readonly EquipmentFuelLogService _service;

    public EquipmentFuelLogsController(
        EquipmentFuelLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentFuelLogResponse>>> GetAll(
        [FromQuery] Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetAllAsync(
                GetCurrentUserId(),
                equipmentId,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentFuelLogResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetByIdAsync(
                GetCurrentUserId(),
                id,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentFuelLogResponse>> Create(
        [FromBody] CreateEquipmentFuelLogRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetAll),
                new { equipmentId = result.EquipmentId },
                result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentFuelLogResponse>> Update(
        Guid id,
        [FromBody] UpdateEquipmentFuelLogRequest request,
        CancellationToken cancellationToken)
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
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
        {
            throw new EquipmentManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}