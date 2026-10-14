using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.Materials;
using CSM.Application.Procurement.Materials.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MaterialsController : ControllerBase
{
    private readonly IMaterialService _materialService;

    public MaterialsController(IMaterialService materialService)
    {
        _materialService = materialService;
    }

    [HttpGet("lookup/categories")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyList<MaterialLookupDto>>> GetCategories(
        CancellationToken cancellationToken)
    {
        return Ok(await _materialService.GetCategoriesAsync(cancellationToken));
    }

    [HttpGet("lookup/units")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyList<MaterialLookupDto>>> GetUnits(
        CancellationToken cancellationToken)
    {
        return Ok(await _materialService.GetUnitsAsync(cancellationToken));
    }

    [HttpGet("lookup/material-types")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyList<MaterialLookupDto>>> GetMaterialTypes(
        CancellationToken cancellationToken)
    {
        return Ok(await _materialService.GetMaterialTypesAsync(cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<IReadOnlyCollection<MaterialResponse>>> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _materialService.GetAllAsync(
                GetCurrentUserId(),
                isActive,
                category,
                cancellationToken);

            return Ok(result);
        }
        catch (MaterialManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{materialId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.View")]
    public async Task<ActionResult<MaterialResponse>> GetById(
        Guid materialId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _materialService.GetByIdAsync(
                GetCurrentUserId(),
                materialId,
                cancellationToken);

            return Ok(result);
        }
        catch (MaterialManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<MaterialResponse>> Create(
        [FromBody] CreateMaterialRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _materialService.CreateAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { materialId = result.Id },
                result);
        }
        catch (MaterialManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{materialId:guid}")]
    [Authorize(Policy = "Procurement.Inventory.Adjust")]
    public async Task<ActionResult<MaterialResponse>> Update(
        Guid materialId,
        [FromBody] UpdateMaterialRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _materialService.UpdateAsync(
                GetCurrentUserId(),
                materialId,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (MaterialManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user identifier is missing.");
        }

        return userId;
    }
}