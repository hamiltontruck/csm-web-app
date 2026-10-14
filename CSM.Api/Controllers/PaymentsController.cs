using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.Payments;
using CSM.Application.Finance.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentReceiptPdfService _paymentReceiptPdfService;

    public PaymentsController(
        IPaymentService paymentService,
        IPaymentReceiptPdfService paymentReceiptPdfService)
    {
        _paymentService = paymentService;
        _paymentReceiptPdfService = paymentReceiptPdfService;
    }

    [HttpGet]
    [Authorize(Policy = "Finance.Payment.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<PaymentResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var payments = await _paymentService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(payments);
        }
        catch (PaymentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{paymentId:guid}")]
    [Authorize(Policy = "Finance.Payment.View")]
    public async Task<ActionResult<PaymentResponse>> GetById(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await _paymentService.GetByIdAsync(
                GetCurrentUserId(),
                paymentId,
                cancellationToken);

            return Ok(payment);
        }
        catch (PaymentManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Finance.Payment.Manage")]
    public async Task<ActionResult<PaymentResponse>> Create(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await _paymentService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { paymentId = payment.Id },
                payment);
        }
        catch (PaymentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = ex.Message,
                detail = ex.InnerException?.Message
            });
        }
    }

    [HttpPut("{paymentId:guid}")]
    [Authorize(Policy = "Finance.Payment.Manage")]
    public async Task<ActionResult<PaymentResponse>> Update(
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await _paymentService.UpdateAsync(
                GetCurrentUserId(),
                paymentId,
                request,
                cancellationToken);

            return Ok(payment);
        }
        catch (PaymentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{paymentId:guid}/status")]
    [Authorize(Policy = "Finance.Payment.Manage")]
    public async Task<ActionResult<PaymentResponse>> ChangeStatus(
        Guid paymentId,
        ChangePaymentStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await _paymentService.ChangeStatusAsync(
                GetCurrentUserId(),
                paymentId,
                request,
                cancellationToken);

            return Ok(payment);
        }
        catch (PaymentManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Finance.Payment.Manage")]
    public async Task<IActionResult> Complete(Guid id)
    {
        await _paymentService.CompletePaymentAsync(
            GetCurrentUserId(),
            id);

        return NoContent();
    }

    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = "Finance.Payment.Manage")]
    public async Task<IActionResult> Reverse(Guid id)
    {
        await _paymentService.ReversePaymentAsync(
            GetCurrentUserId(),
            id);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Finance.Payment.Manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        await _paymentService.DeleteAsync(currentUserId, id);

        return NoContent();
    }

    [HttpGet("company")]
    [Authorize(Policy = "Finance.Payment.View")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentResponse>>> GetCompany(
        CancellationToken cancellationToken)
    {
        var payments = await _paymentService.GetCompanyPaymentsAsync(
            GetCurrentUserId(),
            cancellationToken);

        return Ok(payments);
    }

    [HttpGet("{id:guid}/print")]
    [Authorize(Policy = "Finance.Payment.View")]
    public async Task<IActionResult> Print(
        Guid id,
        CancellationToken cancellationToken)
    {
        var pdf = await _paymentReceiptPdfService.GenerateAsync(
            GetCurrentUserId(),
            id,
            cancellationToken);

        return File(
            pdf,
            "application/pdf",
            $"Receipt-{id}.pdf");
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new PaymentManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
