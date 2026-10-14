using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockReturns;
using CSM.Application.Inventory.StockReturns.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockReturnsController : ControllerBase
{
    private readonly IStockReturnService _stockReturnService;

    public StockReturnsController(
        IStockReturnService stockReturnService)
    {
        _stockReturnService = stockReturnService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyCollection<StockReturnResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockReturnService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (StockReturnManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{stockReturnId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<StockReturnResponse>> GetById(
        Guid stockReturnId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockReturnService.GetByIdAsync(
                GetCurrentUserId(),
                stockReturnId,
                cancellationToken);

            return Ok(result);
        }
        catch (StockReturnManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<StockReturnResponse>> Create(
        CreateStockReturnRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _stockReturnService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (StockReturnManagementException ex)
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
            throw new StockReturnManagementException(
                "Invalid authenticated user.");
        }

        return userId;
    }
}
