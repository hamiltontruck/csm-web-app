using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.Permits;
using CSM.Application.Compliance.Permits.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
public sealed class PermitsController : ControllerBase
{
    private readonly IPermitService _permitService;

    public PermitsController(
        IPermitService permitService)
    {
        _permitService = permitService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<PermitResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _permitService.GetAllAsync(
                    GetCurrentUserId(),
                    cancellationToken);

            return Ok(result);
        }
        catch (PermitManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{permitId:guid}")]
    public async Task<ActionResult<PermitResponse>> GetById(
        Guid permitId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _permitService.GetByIdAsync(
                    GetCurrentUserId(),
                    permitId,
                    cancellationToken);

            return Ok(result);
        }
        catch (PermitManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PermitResponse>> Create(
        [FromBody] CreatePermitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _permitService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { permitId = result.Id },
                result);
        }
        catch (PermitManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{permitId:guid}")]
    public async Task<ActionResult<PermitResponse>> Update(
        Guid permitId,
        [FromBody] UpdatePermitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _permitService.UpdateAsync(
                    GetCurrentUserId(),
                    permitId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (PermitManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                userId,
                out var parsedUserId))
        {
            throw new PermitManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
