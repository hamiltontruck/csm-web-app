using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Finance.PettyCashAccounts;
using CSM.Application.Finance.PettyCashAccounts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Finance.PettyCash.View")]
public sealed class PettyCashAccountsController : ControllerBase
{
    private readonly IPettyCashAccountService _service;

    public PettyCashAccountsController(
        IPettyCashAccountService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PettyCashAccountResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetAllAsync(
                GetCurrentUserId(),
                cancellationToken));
        }
        catch (PettyCashManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpGet("site/{siteId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<PettyCashAccountResponse>>> GetSite(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetSiteAccountsAsync(
                GetCurrentUserId(),
                siteId,
                cancellationToken));
        }
        catch (PettyCashManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PettyCashAccountResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(
                GetCurrentUserId(),
                id,
                cancellationToken));
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
    public async Task<ActionResult<PettyCashAccountResponse>> Create(
        CreatePettyCashAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken));
        }
        catch (PettyCashManagementException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Finance.PettyCash.Manage")]
    public async Task<ActionResult<PettyCashAccountResponse>> Update(
        Guid id,
        UpdatePettyCashAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateAsync(
                GetCurrentUserId(),
                id,
                request,
                cancellationToken));
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