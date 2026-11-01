using System.Security.Claims;
using CSM.Domain.Constants;
using CSM.Api.Controllers;
using CSM.Application.Projects.DailyProgressPhotos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.API.Controllers;

[ApiController]
[Route("api/daily-progress-photos")]
[Authorize(Policy = "Projects.View")]
[ScopedProjectError]
[Authorize(Policy = "Projects.DailyProgress.View")]
public sealed class DailyProgressPhotosController : ControllerBase
{
    private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private readonly IDailyProgressPhotoService _service;

    public DailyProgressPhotosController(
        IDailyProgressPhotoService service)
    {
        _service = service;
    }

    [HttpGet("{diaryId:guid}")]
    public async Task<IActionResult> GetByDiary(
        Guid diaryId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByDiaryAsync(
            CurrentUserId,
            diaryId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{photoId:guid}/content")]
    public async Task<IActionResult> Download(Guid photoId, CancellationToken cancellationToken)
    {
        var content = await _service.DownloadAsync(CurrentUserId, photoId, cancellationToken);
        if (content is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Content, content.ContentType);
    }

    [HttpPost("{diaryId:guid}")]
    [Authorize(Policy = "Projects.DailyProgress.Create")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager + "," + AppRoles.Supervisor)]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Upload(
        Guid diaryId,
        IFormFile file,
        [FromForm] string? caption,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Image is required.");

        await using var stream = file.OpenReadStream();

        var photo = await _service.UploadAsync(
            CurrentUserId,
            diaryId,
            stream,
            file.FileName,
            caption,
            cancellationToken);

        return Ok(photo);
    }

    [HttpDelete("{photoId:guid}")]
    [Authorize(Policy = "Projects.DailyProgress.Create")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," + AppRoles.SiteManager + "," + AppRoles.Supervisor)]
    public async Task<IActionResult> Delete(
        Guid photoId,
        CancellationToken cancellationToken)
    {

        await _service.DeleteAsync(
            CurrentUserId,
            photoId,
            cancellationToken);

        return NoContent();
    }
}