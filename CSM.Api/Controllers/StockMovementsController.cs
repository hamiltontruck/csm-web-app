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
            return MovementError(ex);
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
            return MovementError(ex);
        }
    }

    private ObjectResult MovementError(StockMovementManagementException exception)
    {
        var forbidden = exception.Message is
            "You are not authorized to access stock movements."
            or "You are not authorized to access this stock movement."
            or "Current user is not assigned to a company.";
        return forbidden
            ? StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message })
            : BadRequest(new { message = exception.Message });
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
