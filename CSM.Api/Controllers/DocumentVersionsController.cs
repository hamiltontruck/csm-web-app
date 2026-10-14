using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Documents.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/documents/{documentId:guid}/versions")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
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
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
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
            return BadRequest(new { message = ex.Message });
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
            return BadRequest(new { message = ex.Message });
        }
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