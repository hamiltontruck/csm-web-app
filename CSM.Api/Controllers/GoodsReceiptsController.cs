using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.GoodsReceipts;
using CSM.Application.Procurement.GoodsReceipts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _goodsReceiptService;

    public GoodsReceiptsController(
        IGoodsReceiptService goodsReceiptService)
    {
        _goodsReceiptService = goodsReceiptService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<GoodsReceiptResponse>>> GetAll(
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] Guid? purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _goodsReceiptService.GetAllAsync(
                    GetCurrentUserId(),
                    constructionSiteId,
                    purchaseOrderId,
                    cancellationToken);

            return Ok(result);
        }
        catch (GoodsReceiptManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpGet("{goodsReceiptId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<GoodsReceiptResponse>> GetById(
        Guid goodsReceiptId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _goodsReceiptService.GetByIdAsync(
                    GetCurrentUserId(),
                    goodsReceiptId,
                    cancellationToken);

            return Ok(result);
        }
        catch (GoodsReceiptManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<GoodsReceiptResponse>> Create(
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _goodsReceiptService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    goodsReceiptId = result.Id
                },
                result);
        }
        catch (GoodsReceiptManagementException ex)
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

        if (!Guid.TryParse(value, out var userId))
        {
            throw new GoodsReceiptManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
