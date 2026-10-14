using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.RebarSpecs;
using CSM.Application.Procurement.RebarSpecs.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
public sealed class RebarSpecsController : ControllerBase
{
    private readonly IRebarSpecService _rebarSpecService;

    public RebarSpecsController(
        IRebarSpecService rebarSpecService)
    {
        _rebarSpecService = rebarSpecService;
    }

    [HttpGet("{materialId:guid}")]
    public async Task<ActionResult<RebarSpecResponse>> GetByMaterialId(
        Guid materialId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _rebarSpecService.GetByMaterialIdAsync(
                    GetCurrentUserId(),
                    materialId,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<ActionResult<RebarSpecResponse>> Create(
        [FromBody] CreateRebarSpecRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _rebarSpecService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{materialId:guid}")]
    public async Task<ActionResult<RebarSpecResponse>> Update(
        Guid materialId,
        [FromBody] UpdateRebarSpecRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _rebarSpecService.UpdateAsync(
                    GetCurrentUserId(),
                    materialId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialManagementException ex)
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
            throw new MaterialManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}
