using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.Inspections;
using CSM.Application.Compliance.Inspections.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,CompanyAdmin,SiteManager")]
[Authorize(Policy = "Compliance.View")]
[ComplianceAccessExceptionFilter]
public sealed class InspectionsController : ControllerBase
{
    private readonly IInspectionService _inspectionService;

    public InspectionsController(
        IInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<InspectionResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _inspectionService.GetAllAsync(
                    GetCurrentUserId(),
                    cancellationToken);

            return Ok(result);
        }
        catch (InspectionManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{inspectionId:guid}")]
    public async Task<ActionResult<InspectionResponse>> GetById(
        Guid inspectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _inspectionService.GetByIdAsync(
                    GetCurrentUserId(),
                    inspectionId,
                    cancellationToken);

            return Ok(result);
        }
        catch (InspectionManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<InspectionResponse>> Create(
        [FromBody] CreateInspectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _inspectionService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { inspectionId = result.Id },
                result);
        }
        catch (InspectionManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{inspectionId:guid}")]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<InspectionResponse>> Update(
        Guid inspectionId,
        [FromBody] UpdateInspectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _inspectionService.UpdateAsync(
                    GetCurrentUserId(),
                    inspectionId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (InspectionManagementException ex)
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
            throw new InspectionManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
