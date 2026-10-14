using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.InvoiceLines;
using CSM.Application.Finance.InvoiceLines.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.FinanceManagement)]
public sealed class InvoiceLinesController : ControllerBase
{
    private readonly IInvoiceLineService _invoiceLineService;

    public InvoiceLinesController(
        IInvoiceLineService invoiceLineService)
    {
        _invoiceLineService = invoiceLineService;
    }

    [HttpGet("invoice/{invoiceId:guid}")]
    public async Task<
        ActionResult<IReadOnlyCollection<InvoiceLineResponse>>> GetByInvoice(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var lines = await _invoiceLineService.GetByInvoiceAsync(
                GetCurrentUserId(),
                invoiceId,
                cancellationToken);

            return Ok(lines);
        }
        catch (InvoiceLineManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{invoiceLineId:guid}")]
    public async Task<ActionResult<InvoiceLineResponse>> GetById(
        Guid invoiceLineId,
        CancellationToken cancellationToken)
    {
        try
        {
            var line = await _invoiceLineService.GetByIdAsync(
                GetCurrentUserId(),
                invoiceLineId,
                cancellationToken);

            return Ok(line);
        }
        catch (InvoiceLineManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("invoice/{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceLineResponse>> Create(
        Guid invoiceId,
        CreateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var line = await _invoiceLineService.CreateAsync(
                GetCurrentUserId(),
                invoiceId,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { invoiceLineId = line.Id },
                line);
        }
        catch (InvoiceLineManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{invoiceLineId:guid}")]
    public async Task<ActionResult<InvoiceLineResponse>> Update(
        Guid invoiceLineId,
        UpdateInvoiceLineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var line = await _invoiceLineService.UpdateAsync(
                GetCurrentUserId(),
                invoiceLineId,
                request,
                cancellationToken);

            return Ok(line);
        }
        catch (InvoiceLineManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{invoiceLineId:guid}")]
    public async Task<ActionResult> Delete(
        Guid invoiceLineId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _invoiceLineService.DeleteAsync(
                GetCurrentUserId(),
                invoiceLineId,
                cancellationToken);

            return NoContent();
        }
        catch (InvoiceLineManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvoiceLineManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
