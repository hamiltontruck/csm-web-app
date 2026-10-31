using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Compliance.DocumentCategories;
using CSM.Application.Compliance.DocumentCategories.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
[Authorize(Policy = "Compliance.View")]
public sealed class DocumentCategoriesController : ControllerBase
{
    private readonly IDocumentCategoryService _documentCategoryService;

    public DocumentCategoriesController(
        IDocumentCategoryService documentCategoryService)
    {
        _documentCategoryService = documentCategoryService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<DocumentCategoryResponse>>> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentCategoryService.GetAllAsync(
                    GetCurrentUserId(),
                    isActive,
                    cancellationToken);

            return Ok(result);
        }
        catch (DocumentCategoryManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpGet("{documentCategoryId:guid}")]
    public async Task<ActionResult<DocumentCategoryResponse>> GetById(
        Guid documentCategoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentCategoryService.GetByIdAsync(
                    GetCurrentUserId(),
                    documentCategoryId,
                    cancellationToken);

            return Ok(result);
        }
        catch (DocumentCategoryManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<DocumentCategoryResponse>> Create(
        [FromBody] CreateDocumentCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentCategoryService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { documentCategoryId = result.Id },
                result);
        }
        catch (DocumentCategoryManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{documentCategoryId:guid}")]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<DocumentCategoryResponse>> Update(
        Guid documentCategoryId,
        [FromBody] UpdateDocumentCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentCategoryService.UpdateAsync(
                    GetCurrentUserId(),
                    documentCategoryId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (DocumentCategoryManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    private ActionResult ErrorResult(DocumentCategoryManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to manage document categories." or
            "You are not authorized to access this document category." or
            "Current user is not assigned to a company." or
            "Authenticated user was not found." or
            "Authenticated user identifier is invalid." => StatusCode(403, body),
            "Document category was not found." => NotFound(body),
            _ => BadRequest(body)
        };
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
            throw new DocumentCategoryManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
