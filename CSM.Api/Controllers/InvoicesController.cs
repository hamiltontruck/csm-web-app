using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.Invoices;
using CSM.Application.Finance.Invoices.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoicePdfService _invoicePdfService;

    public InvoicesController(
        IInvoiceService invoiceService,
        IInvoicePdfService invoicePdfService)
    {
        _invoiceService = invoiceService;
        _invoicePdfService = invoicePdfService;
    }

    [HttpGet]
    [Authorize(Policy = "Finance.Invoice.View")]
    public async Task<ActionResult<IReadOnlyCollection<InvoiceResponse>>> GetAll(
        [FromQuery] Guid siteId,
        CancellationToken cancellationToken)
    {
        var invoices = await _invoiceService.GetAllAsync(
            GetCurrentUserId(),
            siteId,
            cancellationToken);

        return Ok(invoices);
    }

    [HttpGet("company")]
    [Authorize(Policy = "Finance.Invoice.View")]
    public async Task<ActionResult<IReadOnlyCollection<InvoiceResponse>>> GetCompany(
        CancellationToken cancellationToken)
    {
        var invoices = await _invoiceService.GetCompanyInvoicesAsync(
            GetCurrentUserId(),
            cancellationToken);

        return Ok(invoices);
    }

    [HttpGet("{invoiceId:guid}")]
    [Authorize(Policy = "Finance.Invoice.View")]
    public async Task<ActionResult<InvoiceResponse>> GetById(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceService.GetByIdAsync(
                GetCurrentUserId(),
                invoiceId,
                cancellationToken);

            return Ok(invoice);
        }
        catch (InvoiceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceResponse>> Create(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { invoiceId = invoice.Id },
                invoice);
        }
        catch (InvoiceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceResponse>> Update(
        Guid invoiceId,
        UpdateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceService.UpdateAsync(
                GetCurrentUserId(),
                invoiceId,
                request,
                cancellationToken);

            return Ok(invoice);
        }
        catch (InvoiceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{invoiceId:guid}/status")]
    public async Task<ActionResult<InvoiceResponse>> ChangeStatus(
        Guid invoiceId,
        ChangeInvoiceStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await _invoiceService.ChangeStatusAsync(
                GetCurrentUserId(),
                invoiceId,
                request,
                cancellationToken);

            return Ok(invoice);
        }
        catch (InvoiceManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/issue")]
    public async Task<ActionResult<InvoiceResponse>> Issue(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var invoice = await _invoiceService.IssueAsync(
            userId,
            id,
            cancellationToken);

        return Ok(invoice);
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<ActionResult<InvoiceResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var invoice = await _invoiceService.CancelAsync(
            GetCurrentUserId(),
            id,
            cancellationToken);

        return Ok(invoice);
    }

    [HttpGet("{id:guid}/print")]
    public async Task<IActionResult> Print(
        Guid id,
        CancellationToken cancellationToken)
    {
        var pdf = await _invoicePdfService.GenerateAsync(
            GetCurrentUserId(),
            id,
            cancellationToken);

        return File(
            pdf,
            "application/pdf",
            $"Invoice-{id}.pdf");
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvoiceManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
