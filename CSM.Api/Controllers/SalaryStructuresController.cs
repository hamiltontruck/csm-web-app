using CSM.Application.Common.Security;
using CSM.Application.Payroll;
using CSM.Application.Payroll.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class SalaryStructuresController : ControllerBase
{
    private readonly ISalaryStructureService _service;

    public SalaryStructuresController(
        ISalaryStructureService service)
    {
        _service = service;
    }

    [HttpGet("{employeeId:guid}")]
    public async Task<ActionResult<SalaryStructureDto>> Get(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByEmployeeAsync(
            employeeId,
            cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SalaryStructureDto>> CreateOrUpdate(
        UpdateSalaryStructureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateOrUpdateAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}