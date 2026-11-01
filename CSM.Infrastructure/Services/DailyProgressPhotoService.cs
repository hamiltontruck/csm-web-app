using CSM.Application.Projects.DailyProgressPhotos;
using CSM.Application.Projects.DailyProgressPhotos.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
namespace CSM.Infrastructure.Services;

public sealed class DailyProgressPhotoService : IDailyProgressPhotoService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    public DailyProgressPhotoService(ApplicationDbContext context, IWebHostEnvironment environment)
        => (_context, _environment) = (context, environment);
    private static string Url(Guid id) => $"/api/daily-progress-photos/{id}/content";
    private static DailyProgressPhotoResponse Map(DailyProgressPhoto p) => new(p.Id, p.DailySiteDiaryId, p.FileName, Url(p.Id), p.Caption, p.UploadedAtUtc);
    public async Task<IReadOnlyCollection<DailyProgressPhotoResponse>> GetByDiaryAsync(Guid currentUserId, Guid diaryId, CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, false, true, cancellationToken);
        var diary = await access.DiaryAsync(diaryId, cancellationToken);
        var photos = await _context.DailyProgressPhotos.AsNoTracking().Where(x => x.DailySiteDiaryId == diaryId && x.CompanyId == diary.CompanyId && !x.IsDeleted).OrderByDescending(x => x.UploadedAtUtc).ToListAsync(cancellationToken);
        return photos.Select(Map).ToArray();
    }
    public async Task<DailyProgressPhotoResponse> UploadAsync(Guid currentUserId, Guid diaryId, Stream fileStream, string fileName, string? caption, CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, true, cancellationToken);
        var diary = await access.DiaryAsync(diaryId, cancellationToken);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) throw new ArgumentException("Unsupported photo type.");
        var uniqueName = $"{Guid.NewGuid()}{extension}";
        var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "daily-progress");
        Directory.CreateDirectory(folder);
        var fullPath = Path.Combine(folder, uniqueName);
        await using (var output = File.Create(fullPath)) await fileStream.CopyToAsync(output, cancellationToken);
        var photo = new DailyProgressPhoto { CompanyId = diary.CompanyId, DailySiteDiaryId = diaryId, FileName = uniqueName,
            FilePath = $"App_Data/daily-progress/{uniqueName}", Caption = caption, UploadedByUserId = currentUserId, UploadedAtUtc = DateTime.UtcNow };
        _context.DailyProgressPhotos.Add(photo);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch { File.Delete(fullPath); throw; }
        return Map(photo);
    }
    public async Task<DailyProgressPhotoContent?> DownloadAsync(Guid currentUserId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await AuthorizedPhotoAsync(currentUserId, photoId, false, cancellationToken);
        if (photo is null) return null;
        var path = PhysicalPath(photo);
        if (!File.Exists(path)) return null;
        var contentType = Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", _ => "application/octet-stream" };
        return new(File.OpenRead(path), contentType, Path.GetFileName(photo.FileName));
    }
    public async Task DeleteAsync(Guid currentUserId, Guid photoId, CancellationToken cancellationToken = default)
    {
        var photo = await AuthorizedPhotoAsync(currentUserId, photoId, true, cancellationToken);
        if (photo is null) return;
        // Preserve legacy files; the authorized soft delete removes API visibility.
        photo.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);
    }
    private async Task<DailyProgressPhoto?> AuthorizedPhotoAsync(Guid actor, Guid id, bool write, CancellationToken ct)
    {
        var access = await ProjectChildAccess.ForAsync(_context, actor, write, true, ct);
        return await _context.DailyProgressPhotos.SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted && access.Diaries.Any(d => d.Id == p.DailySiteDiaryId && d.CompanyId == p.CompanyId), ct);
    }
    private string PhysicalPath(DailyProgressPhoto photo)
    {
        // Only application-generated basenames within the two known storage roots are valid.
        var name = Path.GetFileName(photo.FilePath.Replace('\\', '/'));
        if (string.IsNullOrEmpty(name) || name != photo.FileName || name.Contains("..")) throw new ArgumentException("Invalid photo path.");
        var legacy = photo.FilePath.StartsWith("/uploads/daily-progress/", StringComparison.Ordinal);
        if (!legacy && !photo.FilePath.StartsWith("App_Data/daily-progress/", StringComparison.Ordinal)) throw new ArgumentException("Invalid photo path.");
        return legacy ? Path.Combine(_environment.WebRootPath, "uploads", "daily-progress", name) : Path.Combine(_environment.ContentRootPath, "App_Data", "daily-progress", name);
    }
}
