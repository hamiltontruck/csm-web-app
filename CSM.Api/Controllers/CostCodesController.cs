using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.CostCodes;
using CSM.Application.Finance.CostCodes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CostCodesController : ControllerBase
{
    private readonly ICostCodeService _costCodeService;

    public CostCodesController(
        ICostCodeService costCodeService)
    {
        _costCodeService = costCodeService;
    }

    [HttpGet]
    [Authorize(Policy = "Finance.CostCode.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<CostCodeResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var costCodes = await _costCodeService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(costCodes);
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{costCodeId:guid}")]
    [Authorize(Policy = "Finance.CostCode.View")]
    public async Task<ActionResult<CostCodeResponse>> GetById(
        Guid costCodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var costCode = await _costCodeService.GetByIdAsync(
                GetCurrentUserId(),
                costCodeId,
                cancellationToken);

            return Ok(costCode);
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Finance.CostCode.Manage")]
    public async Task<ActionResult<CostCodeResponse>> Create(
        CreateCostCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var costCode = await _costCodeService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { costCodeId = costCode.Id },
                costCode);
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{costCodeId:guid}")]
    [Authorize(Policy = "Finance.CostCode.Manage")]
    public async Task<ActionResult<CostCodeResponse>> Update(
        Guid costCodeId,
        UpdateCostCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var costCode = await _costCodeService.UpdateAsync(
                GetCurrentUserId(),
                costCodeId,
                request,
                cancellationToken);

            return Ok(costCode);
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{costCodeId:guid}/active-status")]
    [Authorize(Policy = "Finance.CostCode.Manage")]
    public async Task<ActionResult<CostCodeResponse>> ChangeActiveStatus(
        Guid costCodeId,
        ChangeCostCodeActiveStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var costCode = await _costCodeService.ChangeActiveStatusAsync(
                GetCurrentUserId(),
                costCodeId,
                request,
                cancellationToken);

            return Ok(costCode);
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("active")]
    [Authorize(Policy = "Finance.CostCode.View")]
    public async Task<ActionResult<IReadOnlyCollection<CostCodeResponse>>> GetActive(
        CancellationToken cancellationToken)
    {
        try
        {
            var costCodes = await _costCodeService.GetActiveAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(costCodes);
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("~/api/ConstructionSites/{siteId:guid}/cost-codes")]
    [Authorize(Policy = "Finance.CostCode.View")]
    public async Task<ActionResult<List<CostCodeResponse>>> GetBySite(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var result = await _costCodeService.GetBySiteAsync(
            siteId,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Finance.CostCode.Manage")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await _costCodeService.DeleteAsync(
                GetCurrentUserId(),
                id,
                cancellationToken);

            return NoContent();
        }
        catch (CostCodeManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new CostCodeManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
