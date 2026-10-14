using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockTransfers;
using CSM.Application.Inventory.StockTransfers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _stockTransferService;

    public StockTransfersController(
        IStockTransferService stockTransferService)
    {
        _stockTransferService = stockTransferService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<List<StockTransferResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _stockTransferService.GetAllAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<StockTransferResponse>> Create(
        CreateStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockTransferService.CreateAsync(
                GetCurrentCompanyId(),
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (StockTransferManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    private Guid GetCurrentCompanyId()
    {
        var value = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(value, out var companyId))
        {
            throw new StockTransferManagementException(
                "Invalid authenticated company.");
        }

        return companyId;
    }
}
