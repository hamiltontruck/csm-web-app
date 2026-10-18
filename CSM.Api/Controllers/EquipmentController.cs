using System.Security.Claims;
using CSM.Domain.Constants;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.Equipment;
using CSM.Application.Procurement.Equipment.Dtos;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
public sealed class EquipmentController : ControllerBase
{
    private readonly IEquipmentService _equipmentService;

    public EquipmentController(
        IEquipmentService equipmentService)
    {
        _equipmentService = equipmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EquipmentResponse>>> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] EquipmentStatus? status,
        [FromQuery] EquipmentOwnershipType? ownershipType,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _equipmentService.GetAllAsync(
                GetCurrentUserId(),
                isActive,
                status,
                ownershipType,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{equipmentId:guid}")]
    public async Task<ActionResult<EquipmentResponse>> GetById(
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _equipmentService.GetByIdAsync(
                GetCurrentUserId(),
                equipmentId,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentResponse>> Create(
        [FromBody] CreateEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _equipmentService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    equipmentId = result.Id
                },
                result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{equipmentId:guid}")]
    public async Task<ActionResult<EquipmentResponse>> Update(
        Guid equipmentId,
        [FromBody] UpdateEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _equipmentService.UpdateAsync(
                GetCurrentUserId(),
                equipmentId,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
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
            throw new EquipmentManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}
