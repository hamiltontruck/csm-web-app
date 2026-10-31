using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentTimesheets;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Procurement.Inventory.View")]
public sealed class EquipmentTimesheetsController : ControllerBase
{
    private readonly EquipmentTimesheetService _service;

    public EquipmentTimesheetsController(
        EquipmentTimesheetService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentTimesheetResponse>>> GetAll(
        [FromQuery] Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(
            GetCurrentCompanyId(),
            equipmentId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentTimesheetResponse>> Create(
        CreateEquipmentTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(
            GetCurrentCompanyId(),
            request,
            cancellationToken);

        return Ok(result);
    }

    private Guid GetCurrentCompanyId()
    {
        var value = User.FindFirstValue("company_id");

        if (!Guid.TryParse(value, out var companyId))
            throw new EquipmentManagementException(
                "Current company identity is invalid.");

        return companyId;
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentTimesheetResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            GetCurrentCompanyId(),
            id,
            cancellationToken);

        return Ok(result);
    }
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<EquipmentTimesheetResponse>> Update(
        Guid id,
        CreateEquipmentTimesheetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(
            GetCurrentCompanyId(),
            id,
            request,
            cancellationToken);

        return Ok(result);
    }
}