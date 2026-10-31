using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Documents.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CSM.Api.Models.Documents;



namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
[Authorize(Policy = "Compliance.View")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(
        IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<DocumentResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentService.GetAllAsync(
                    GetCurrentUserId(),
                    cancellationToken);

            return Ok(result);
        }
        catch (DocumentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpGet("{documentId:guid}")]
    public async Task<ActionResult<DocumentResponse>> GetById(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentService.GetByIdAsync(
                    GetCurrentUserId(),
                    documentId,
                    cancellationToken);

            return Ok(result);
        }
        catch (DocumentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<DocumentResponse>> Create(
        [FromBody] CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { documentId = result.Id },
                result);
        }
        catch (DocumentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{documentId:guid}")]
    [Authorize(Policy = "Compliance.Manage")]
    public async Task<ActionResult<DocumentResponse>> Update(
        Guid documentId,
        [FromBody] UpdateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _documentService.UpdateAsync(
                    GetCurrentUserId(),
                    documentId,
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (DocumentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }
    [HttpPost("{id:guid}/upload-version")]
    [Authorize(Policy = "Compliance.Manage")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<DocumentVersionResponse>> UploadVersion(
        Guid id,
        [FromForm] UploadDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = request.File.OpenReadStream();
            var result = await _documentService.UploadVersionAsync(
                GetCurrentUserId(),
                id,
                stream,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                request.RevisionNotes,
                cancellationToken);
            return Ok(result);
        }
        catch (DocumentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }






    private ActionResult ErrorResult(DocumentManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to manage documents." or
            "You are not authorized to access this document." or
            "You can only access documents in your own company." or
            "Current user is not assigned to a company." or
            "Current user not found." or
            "Authenticated user was not found." or
            "Authenticated user identifier is invalid." => StatusCode(403, body),
            "Document was not found." or "Document not found." or
            "Document version not found." or "Document version file was not found." => NotFound(body),
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
            throw new DocumentManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}
