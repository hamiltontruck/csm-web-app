using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.PurchaseOrders;
using CSM.Application.Procurement.PurchaseOrders.Dtos;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(
        IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.PurchaseOrder.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<PurchaseOrderResponse>>> GetAll(
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] Guid? vendorId,
        [FromQuery] PurchaseOrderStatus? status,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.GetAllAsync(
                    GetCurrentUserId(),
                    constructionSiteId,
                    vendorId,
                    status,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{purchaseOrderId:guid}")]
    [Authorize(Policy = "Procurement.PurchaseOrder.View")]
    public async Task<ActionResult<PurchaseOrderResponse>> GetById(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.GetByIdAsync(
                    GetCurrentUserId(),
                    purchaseOrderId,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.PurchaseOrder.Manage")]
    public async Task<ActionResult<PurchaseOrderResponse>> Create(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { purchaseOrderId = result.Id },
                result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{purchaseOrderId:guid}")]
    [Authorize(Policy = "Procurement.PurchaseOrder.Manage")]
    public async Task<ActionResult<PurchaseOrderResponse>> Update(
        Guid purchaseOrderId,
        [FromBody] UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.UpdateAsync(
                    GetCurrentUserId(),
                    purchaseOrderId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{purchaseOrderId:guid}/submit")]
    [Authorize(Policy = "Procurement.PurchaseOrder.Manage")]
    public async Task<ActionResult<PurchaseOrderResponse>> Submit(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.SubmitAsync(
                    GetCurrentUserId(),
                    purchaseOrderId,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{purchaseOrderId:guid}/approve")]
    [Authorize(Policy = "Procurement.PurchaseOrder.Approve")]
    public async Task<ActionResult<PurchaseOrderResponse>> Approve(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.ApproveAsync(
                    GetCurrentUserId(),
                    purchaseOrderId,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{purchaseOrderId:guid}/send-to-vendor")]
    [Authorize(Policy = "Procurement.PurchaseOrder.Manage")]
    public async Task<ActionResult<PurchaseOrderResponse>> SendToVendor(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.SendToVendorAsync(
                    GetCurrentUserId(),
                    purchaseOrderId,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{purchaseOrderId:guid}/cancel")]
    [Authorize(Policy = "Procurement.PurchaseOrder.Manage")]
    public async Task<ActionResult<PurchaseOrderResponse>> Cancel(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _purchaseOrderService.CancelAsync(
                    GetCurrentUserId(),
                    purchaseOrderId,
                    cancellationToken);

            return Ok(result);
        }
        catch (PurchaseOrderManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new PurchaseOrderManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
