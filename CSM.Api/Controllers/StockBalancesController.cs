using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockBalances;
using CSM.Application.Inventory.StockBalances.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockBalancesController : ControllerBase
{
    private readonly IStockBalanceService _stockBalanceService;

    public StockBalancesController(
        IStockBalanceService stockBalanceService)
    {
        _stockBalanceService = stockBalanceService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<StockBalanceResponse>>> GetAll(
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] Guid? materialId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _stockBalanceService.GetAllAsync(
                    GetCurrentUserId(),
                    constructionSiteId,
                    materialId,
                    cancellationToken);

            return Ok(result);
        }
        catch (StockBalanceManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{stockItemId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<StockBalanceResponse>> GetById(
        Guid stockItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _stockBalanceService.GetByIdAsync(
                    GetCurrentUserId(),
                    stockItemId,
                    cancellationToken);

            return Ok(result);
        }
        catch (StockBalanceManagementException ex)
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
            throw new StockBalanceManagementException(
                "Invalid authenticated user.");
        }

        return userId;
    }
}
