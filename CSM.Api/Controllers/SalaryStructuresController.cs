using System.Security.Claims;
using CSM.Application.Payroll;
using CSM.Application.Payroll.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "HRM.Payroll.View")]
[Authorize(Roles = "Administrator,HrOfficer,HROfficer,SuperAdmin,CompanyAdmin")]
public sealed class SalaryStructuresController : ControllerBase
{
    private readonly ISalaryStructureService _service;

    public SalaryStructuresController(ISalaryStructureService service)
    {
        _service = service;
    }

    [HttpGet("{employeeId:guid}")]
    public async Task<ActionResult<SalaryStructureDto>> Get(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetByEmployeeAsync(GetCurrentUserId(), employeeId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [Authorize(Roles = "HrOfficer,HROfficer,SuperAdmin,CompanyAdmin")]
    public async Task<ActionResult<SalaryStructureDto>> CreateOrUpdate(
        UpdateSalaryStructureRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.CreateOrUpdateAsync(GetCurrentUserId(), request, cancellationToken));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private Guid GetCurrentUserId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("Payroll access denied.");
        return id;
    }
}
