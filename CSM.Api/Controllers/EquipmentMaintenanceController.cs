using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.EquipmentMaintenance;
using CSM.Application.Procurement.EquipmentMaintenance.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
public sealed class EquipmentMaintenanceController : ControllerBase
{
    private readonly IEquipmentMaintenanceService
        _equipmentMaintenanceService;

    public EquipmentMaintenanceController(
        IEquipmentMaintenanceService equipmentMaintenanceService)
    {
        _equipmentMaintenanceService =
            equipmentMaintenanceService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<EquipmentMaintenanceResponse>>> GetAll(
        [FromQuery] Guid? equipmentId,
        [FromQuery] bool? completedOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var records =
                await _equipmentMaintenanceService.GetAllAsync(
                    GetCurrentUserId(),
                    equipmentId,
                    completedOnly,
                    cancellationToken);

            return Ok(records);
        }
        catch (EquipmentMaintenanceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{maintenanceId:guid}")]
    public async Task<ActionResult<EquipmentMaintenanceResponse>> GetById(
        Guid maintenanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _equipmentMaintenanceService.GetByIdAsync(
                    GetCurrentUserId(),
                    maintenanceId,
                    cancellationToken);

            return Ok(record);
        }
        catch (EquipmentMaintenanceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentMaintenanceResponse>> Create(
        [FromBody] CreateEquipmentMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _equipmentMaintenanceService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    maintenanceId = record.Id
                },
                record);
        }
        catch (EquipmentMaintenanceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{maintenanceId:guid}")]
    public async Task<ActionResult<EquipmentMaintenanceResponse>> Update(
        Guid maintenanceId,
        [FromBody] UpdateEquipmentMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _equipmentMaintenanceService.UpdateAsync(
                    GetCurrentUserId(),
                    maintenanceId,
                    request,
                    cancellationToken);

            return Ok(record);
        }
        catch (EquipmentMaintenanceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new EquipmentMaintenanceManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}
