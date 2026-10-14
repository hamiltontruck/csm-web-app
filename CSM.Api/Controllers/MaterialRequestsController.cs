using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.MaterialRequests;
using CSM.Application.Procurement.MaterialRequests.Dtos;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MaterialRequestsController : ControllerBase
{
    private readonly IMaterialRequestService _materialRequestService;

    public MaterialRequestsController(
        IMaterialRequestService materialRequestService)
    {
        _materialRequestService = materialRequestService;
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.MaterialRequest.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<MaterialRequestResponse>>> GetAll(
        [FromQuery] Guid? constructionSiteId,
        [FromQuery] Guid? projectId,
        [FromQuery] MaterialRequestStatus? status,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.GetAllAsync(
                    GetCurrentUserId(),
                    constructionSiteId,
                    projectId,
                    status,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet("{materialRequestId:guid}")]
    [Authorize(Policy = "Procurement.MaterialRequest.View")]
    public async Task<ActionResult<MaterialRequestResponse>> GetById(
        Guid materialRequestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.GetByIdAsync(
                    GetCurrentUserId(),
                    materialRequestId,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.MaterialRequest.Submit")]
    public async Task<ActionResult<MaterialRequestResponse>> Create(
        [FromBody] CreateMaterialRequestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    materialRequestId = result.Id
                },
                result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{materialRequestId:guid}")]
    [Authorize(Policy = "Procurement.MaterialRequest.Submit")]
    public async Task<ActionResult<MaterialRequestResponse>> Update(
        Guid materialRequestId,
        [FromBody] UpdateMaterialRequestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.UpdateAsync(
                    GetCurrentUserId(),
                    materialRequestId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{materialRequestId:guid}/submit")]
    [Authorize(Policy = "Procurement.MaterialRequest.Submit")]
    public async Task<ActionResult<MaterialRequestResponse>> Submit(
        Guid materialRequestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.SubmitAsync(
                    GetCurrentUserId(),
                    materialRequestId,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{materialRequestId:guid}/approve")]
    [Authorize(Policy = "Procurement.MaterialRequest.Approve")]
    public async Task<ActionResult<MaterialRequestResponse>> Approve(
        Guid materialRequestId,
        [FromBody] ApproveMaterialRequestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.ApproveAsync(
                    GetCurrentUserId(),
                    materialRequestId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{materialRequestId:guid}/reject")]
    [Authorize(Policy = "Procurement.MaterialRequest.Approve")]
    public async Task<ActionResult<MaterialRequestResponse>> Reject(
        Guid materialRequestId,
        [FromBody] MaterialRequestRemarksRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.RejectAsync(
                    GetCurrentUserId(),
                    materialRequestId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{materialRequestId:guid}/cancel")]
    [Authorize(Policy = "Procurement.MaterialRequest.Submit")]
    public async Task<ActionResult<MaterialRequestResponse>> Cancel(
        Guid materialRequestId,
        [FromBody] MaterialRequestRemarksRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _materialRequestService.CancelAsync(
                    GetCurrentUserId(),
                    materialRequestId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (MaterialRequestManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new MaterialRequestManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}
