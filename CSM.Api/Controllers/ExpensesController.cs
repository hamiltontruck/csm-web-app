using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.Expenses;
using CSM.Application.Finance.Expenses.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(
        IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    [Authorize(Policy = "Finance.Expense.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<ExpenseResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var expenses = await _expenseService.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(expenses);
        }
        catch (ExpenseManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpGet("site/{constructionSiteId:guid}")]
    [Authorize(Policy = "Finance.Expense.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<ExpenseResponse>>> GetBySite(
        Guid constructionSiteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var expenses = await _expenseService.GetBySiteAsync(
                GetCurrentUserId(),
                constructionSiteId,
                cancellationToken);

            return Ok(expenses);
        }
        catch (ExpenseManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpGet("{expenseId:guid}")]
    [Authorize(Policy = "Finance.Expense.View")]
    public async Task<ActionResult<ExpenseResponse>> GetById(
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var expense = await _expenseService.GetByIdAsync(
                GetCurrentUserId(),
                expenseId,
                cancellationToken);

            return Ok(expense);
        }
        catch (ExpenseManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Finance.Expense.Submit")]
    public async Task<ActionResult<ExpenseResponse>> Create(
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var expense = await _expenseService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { expenseId = expense.Id },
                expense);
        }
        catch (ExpenseManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPut("{expenseId:guid}")]
    [Authorize(Policy = "Finance.Expense.Submit")]
    public async Task<ActionResult<ExpenseResponse>> Update(
        Guid expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var expense = await _expenseService.UpdateAsync(
                GetCurrentUserId(),
                expenseId,
                request,
                cancellationToken);

            return Ok(expense);
        }
        catch (ExpenseManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPut("{expenseId:guid}/status")]
    [Authorize(Policy = "Finance.Expense.Approve")]
    public async Task<ActionResult<ExpenseResponse>> ChangeStatus(
        Guid expenseId,
        ChangeExpenseStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var expense = await _expenseService.ChangeStatusAsync(
                GetCurrentUserId(),
                expenseId,
                request,
                cancellationToken);

            return Ok(expense);
        }
        catch (ExpenseManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new ExpenseManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}
