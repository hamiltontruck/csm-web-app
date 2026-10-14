using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.CompanySettings;
using CSM.Application.CompanySettings.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/company/settings")]
[Authorize(Policy = AppPolicies.CompanyManagement)]
public sealed class CompanySettingsController : ControllerBase
{
    private readonly ICompanySettingsService _service;

    public CompanySettingsController(
        ICompanySettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<GetCompanySettingsResponse>> Get(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetAsync(
                GetCurrentUserId(),
                cancellationToken));
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<GetCompanySettingsResponse>> Update(
        UpdateCompanySettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken));
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{companyId:guid}")]
    public async Task<ActionResult<GetCompanySettingsResponse>> GetByCompanyId(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetByCompanyIdAsync(
            companyId,
            cancellationToken));
    }



    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new CompanyManagementException(
                "Invalid authenticated user.");
        }

        return userId;
    }
}