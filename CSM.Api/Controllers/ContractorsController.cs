using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.Contractors;
using CSM.Application.HRM.Contractors.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.HRManagement)]
public sealed class ContractorsController : ControllerBase
{
    private readonly IContractorService _contractorService;

    public ContractorsController(
        IContractorService contractorService)
    {
        _contractorService = contractorService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<ContractorResponse>>> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            var contractors =
                await _contractorService.GetAllAsync(
                    GetCurrentUserId(),
                    isActive,
                    cancellationToken);

            return Ok(contractors);
        }
        catch (ContractorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{contractorId:guid}")]
    public async Task<ActionResult<ContractorResponse>> GetById(
        Guid contractorId,
        CancellationToken cancellationToken)
    {
        try
        {
            var contractor =
                await _contractorService.GetByIdAsync(
                    GetCurrentUserId(),
                    contractorId,
                    cancellationToken);

            return Ok(contractor);
        }
        catch (ContractorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ContractorResponse>> Create(
        CreateContractorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var contractor =
                await _contractorService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    contractorId = contractor.Id
                },
                contractor);
        }
        catch (ContractorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{contractorId:guid}")]
    public async Task<ActionResult<ContractorResponse>> Update(
        Guid contractorId,
        UpdateContractorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var contractor =
                await _contractorService.UpdateAsync(
                    GetCurrentUserId(),
                    contractorId,
                    request,
                    cancellationToken);

            return Ok(contractor);
        }
        catch (ContractorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new ContractorManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
