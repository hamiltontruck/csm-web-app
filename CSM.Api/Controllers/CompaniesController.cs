using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Companies;
using CSM.Application.Companies.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CSM.Application.CompanySettings;
using CSM.Application.CompanySettings.Dtos;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = CSM.Domain.Constants.AppRoles.SuperAdmin)]
public sealed class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ICompanySettingsService _companySettingsService;

    public CompaniesController(
        ICompanyService companyService,
        ICompanySettingsService companySettingsService)
    {
        _companyService = companyService;
        _companySettingsService = companySettingsService;
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CompanyResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var companies =
                await _companyService.GetAllAsync(
                    cancellationToken);

            return Ok(companies);
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<IReadOnlyList<CompanyLookupDto>>> GetLookup(
        CancellationToken cancellationToken)
    {
        var companies = await _companySettingsService
            .GetCompaniesAsync(cancellationToken);

        return Ok(companies);
    }

    [HttpGet("{companyId:guid}")]
    public async Task<ActionResult<CompanyResponse>> GetById(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var company =
                await _companyService.GetByIdAsync(
                    companyId,
                    cancellationToken);

            return Ok(company);
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(
        CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var company =
                await _companyService.CreateAsync(
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { companyId = company.Id },
                company);
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{companyId:guid}")]
    public async Task<ActionResult<CompanyResponse>> Update(
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var company =
                await _companyService.UpdateAsync(
                    companyId,
                    request,
                    cancellationToken);

            return Ok(company);
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{companyId:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _companyService.SetActiveStatusAsync(
                companyId,
                true,
                cancellationToken);

            return NoContent();
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{companyId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _companyService.SetActiveStatusAsync(
                companyId,
                false,
                cancellationToken);

            return NoContent();
        }
        catch (CompanyManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}