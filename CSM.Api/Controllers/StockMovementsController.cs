using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockMovements;
using CSM.Application.Inventory.StockMovements.Dtos;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockMovementsController : ControllerBase
{
    private readonly IStockMovementService _stockMovementService;

    public StockMovementsController(
        IStockMovementService stockMovementService)
    {
        _stockMovementService = stockMovementService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<StockMovementResponse>>> GetAll(
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] Guid? materialId,
        [FromQuery] StockMovementType? movementType,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _stockMovementService.GetAllAsync(
                    GetCurrentUserId(),
                    constructionSiteId,
                    materialId,
                    movementType,
                    fromUtc,
                    toUtc,
                    cancellationToken);

            return Ok(result);
        }
        catch (StockMovementManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{stockMovementId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<StockMovementResponse>> GetById(
        Guid stockMovementId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _stockMovementService.GetByIdAsync(
                    GetCurrentUserId(),
                    stockMovementId,
                    cancellationToken);

            return Ok(result);
        }
        catch (StockMovementManagementException ex)
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
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new StockMovementManagementException(
                "Invalid authenticated user.");
        }

        return userId;
    }
}
