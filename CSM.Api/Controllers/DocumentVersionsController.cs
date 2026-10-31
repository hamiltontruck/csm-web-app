using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Documents.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/documents/{documentId:guid}/versions")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager)]
[Authorize(Policy = "Compliance.View")]
public sealed class DocumentVersionsController : ControllerBase
{
    private readonly IDocumentVersionService _service;

    public DocumentVersionsController(
        IDocumentVersionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DocumentVersionResponse>>> GetAll(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetByDocumentAsync(
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
    public async Task<ActionResult<DocumentVersionResponse>> Create(
        Guid documentId,
        CreateDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(
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

    [HttpGet("{versionId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.DownloadAsync(
                GetCurrentUserId(),
                documentId,
                versionId,
                cancellationToken);

            return File(
                result.Stream,
                result.ContentType ?? "application/octet-stream",
                result.FileName,
                enableRangeProcessing: true);
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
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new DocumentManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}