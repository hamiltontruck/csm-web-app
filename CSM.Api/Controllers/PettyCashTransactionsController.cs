using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.PettyCashTransactions;
using CSM.Application.Finance.PettyCashTransactions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Finance.PettyCash.View")]
public sealed class PettyCashTransactionsController : ControllerBase
{
    private readonly IPettyCashTransactionService _service;

    public PettyCashTransactionsController(
        IPettyCashTransactionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PettyCashTransactionResponse>>> GetAll(
        [FromQuery] Guid pettyCashAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetAllAsync(
                GetCurrentUserId(),
                pettyCashAccountId,
                cancellationToken);

            return Ok(result);
        }
        catch (PettyCashManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Finance.PettyCash.Manage")]
    public async Task<ActionResult<PettyCashTransactionResponse>> Create(
        CreatePettyCashTransactionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (PettyCashManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new PettyCashManagementException(
                "Invalid authenticated user.");
        }

        return userId;
    }
}