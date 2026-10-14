using CSM.Application.Projects.DailyProgressPhotos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.API.Controllers;

[ApiController]
[Route("api/daily-progress-photos")]
[Authorize]
public sealed class DailyProgressPhotosController : ControllerBase
{
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
            Guid.Empty,
            diaryId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{diaryId:guid}")]
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
            Guid.Empty,
            diaryId,
            stream,
            file.FileName,
            caption,
            cancellationToken);

        return Ok(photo);
    }

    [HttpDelete("{photoId:guid}")]
    public async Task<IActionResult> Delete(
        Guid photoId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"DELETE PHOTO: {photoId}");

        await _service.DeleteAsync(
            Guid.Empty,
            photoId,
            cancellationToken);

        return NoContent();
    }
}