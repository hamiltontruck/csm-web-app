using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.DailyMaterialUsage;
using CSM.Application.Inventory.DailyMaterialUsage.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DailyMaterialUsagesController : ControllerBase
{
    private readonly IDailyMaterialUsageService
        _dailyMaterialUsageService;

    public DailyMaterialUsagesController(
        IDailyMaterialUsageService dailyMaterialUsageService)
    {
        _dailyMaterialUsageService =
            dailyMaterialUsageService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyCollection<DailyMaterialUsageResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _dailyMaterialUsageService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (DailyMaterialUsageManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<DailyMaterialUsageResponse>> Create(
        [FromBody] CreateDailyMaterialUsageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _dailyMaterialUsageService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (
            DailyMaterialUsageManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
