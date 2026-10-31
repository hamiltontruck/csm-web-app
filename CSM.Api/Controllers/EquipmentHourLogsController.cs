using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentHourLogs;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CSM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Authorize(Policy = "Procurement.Inventory.View")]
public class EquipmentHourLogsController : ControllerBase
{
    private readonly EquipmentHourLogService _service;

    public EquipmentHourLogsController(EquipmentHourLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentHourLogResponse>>> GetAll(
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
    public async Task<ActionResult<EquipmentHourLogResponse>> Create(
        CreateEquipmentHourLogRequest request,
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
        var value = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(value, out var companyId))
        {
            throw new EquipmentManagementException(
                "Current company identity is invalid.");
        }

        return companyId;
    }
}