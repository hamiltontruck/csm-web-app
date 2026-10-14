using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.Vendors;
using CSM.Application.Procurement.Vendors.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
public sealed class VendorsController : ControllerBase
{
    private readonly IVendorService _vendorService;

    public VendorsController(
        IVendorService vendorService)
    {
        _vendorService = vendorService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<VendorResponse>>> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _vendorService.GetAllAsync(
                    GetCurrentUserId(),
                    isActive,
                    cancellationToken);

            return Ok(result);
        }
        catch (VendorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{vendorId:guid}")]
    public async Task<ActionResult<VendorResponse>> GetById(
        Guid vendorId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _vendorService.GetByIdAsync(
                    GetCurrentUserId(),
                    vendorId,
                    cancellationToken);

            return Ok(result);
        }
        catch (VendorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<VendorResponse>> Create(
        [FromBody] CreateVendorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _vendorService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { vendorId = result.Id },
                result);
        }
        catch (VendorManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{vendorId:guid}")]
    public async Task<ActionResult<VendorResponse>> Update(
        Guid vendorId,
        [FromBody] UpdateVendorRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _vendorService.UpdateAsync(
                    GetCurrentUserId(),
                    vendorId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (VendorManagementException ex)
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
            throw new VendorManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
