using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockAdjustments;
using CSM.Application.Inventory.StockAdjustments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockAdjustmentsController : ControllerBase
{
    private readonly IStockAdjustmentService _stockAdjustmentService;

    public StockAdjustmentsController(
        IStockAdjustmentService stockAdjustmentService)
    {
        _stockAdjustmentService = stockAdjustmentService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<List<StockAdjustmentResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockAdjustmentService.GetAllAsync(
                GetCurrentUserId(), cancellationToken);
            return Ok(result);
        }
        catch (StockAdjustmentManagementException ex)
        {
            return AdjustmentError(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<StockAdjustmentResponse>> Create(
        CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _stockAdjustmentService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (StockAdjustmentManagementException ex)
        {
            return AdjustmentError(ex);
        }
    }

    private ObjectResult AdjustmentError(StockAdjustmentManagementException exception)
    {
        var forbidden = exception.Message is
            "You are not authorized to manage stock adjustments."
            or "You are not authorized to access this company."
            or "Current user is not assigned to a company.";
        return StatusCode(forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest,
            new { message = exception.Message });
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
            throw new StockAdjustmentManagementException(
                "Invalid authenticated user.");
        }

        return userId;
    }
}
