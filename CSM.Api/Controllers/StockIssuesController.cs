using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockIssues;
using CSM.Application.Inventory.StockIssues.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockIssuesController : ControllerBase
{
    private readonly IStockIssueService _stockIssueService;

    public StockIssuesController(
        IStockIssueService stockIssueService)
    {
        _stockIssueService = stockIssueService;
    }

    // GET: api/StockIssues
    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyCollection<StockIssueResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockIssueService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (StockIssueManagementException ex)
        {
            return IssueError(ex);
        }
    }

    // GET: api/StockIssues/{stockMovementId}
    [HttpGet("{stockMovementId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<StockIssueResponse>> GetById(
        Guid stockMovementId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockIssueService.GetByIdAsync(
                GetCurrentUserId(),
                stockMovementId,
                cancellationToken);

            return Ok(result);
        }
        catch (StockIssueManagementException ex)
        {
            return IssueError(ex);
        }
    }

    // POST: api/StockIssues
    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<StockIssueResponse>> Create(
        [FromBody] CreateStockIssueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockIssueService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (StockIssueManagementException ex)
        {
            return IssueError(ex);
        }
    }

    private ObjectResult IssueError(StockIssueManagementException ex)
    {
        var status = ex.Message switch
        {
            "You are not authorized to manage stock issues." or
            "You are not authorized to access this company stock." or
            "Current user is not assigned to a company." => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { message = ex.Message });
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}