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

    public DailyProgressPhotoService(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IReadOnlyCollection<DailyProgressPhotoResponse>> GetByDiaryAsync(
        Guid companyId,
        Guid diaryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailyProgressPhotos
            .AsNoTracking()
            .Where(x =>
                x.DailySiteDiaryId == diaryId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new DailyProgressPhotoResponse(
                x.Id,
                x.DailySiteDiaryId,
                x.FileName,
                x.FilePath,
                x.Caption,
                x.UploadedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<DailyProgressPhotoResponse> UploadAsync(
        Guid companyId,
        Guid diaryId,
        Stream fileStream,
        string fileName,
        string? caption,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid()}{extension}";

        var uploadFolder = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "daily-progress");

        Directory.CreateDirectory(uploadFolder);

        var fullPath = Path.Combine(uploadFolder, uniqueName);

        await using (var output = File.Create(fullPath))
        {
            await fileStream.CopyToAsync(output, cancellationToken);
        }

        var photo = new DailyProgressPhoto
        {
            CompanyId = companyId,
            DailySiteDiaryId = diaryId,
            FileName = uniqueName,
            FilePath = $"/uploads/daily-progress/{uniqueName}",
            Caption = caption,
            UploadedByUserId = Guid.Empty,
            UploadedAtUtc = DateTime.UtcNow
        };

        _context.DailyProgressPhotos.Add(photo);
        await _context.SaveChangesAsync(cancellationToken);

        return new DailyProgressPhotoResponse(
            photo.Id,
            photo.DailySiteDiaryId,
            photo.FileName,
            photo.FilePath,
            photo.Caption,
            photo.UploadedAtUtc);
    }

    public async Task DeleteAsync(
        Guid companyId,
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var photo = await _context.DailyProgressPhotos
            .FirstOrDefaultAsync(x => x.Id == photoId, cancellationToken);

        if (photo == null)
            return;

        var relativePath = photo.FilePath.TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var physicalPath = Path.Combine(
            _environment.WebRootPath,
            relativePath);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        _context.DailyProgressPhotos.Remove(photo);
        await _context.SaveChangesAsync(cancellationToken);
    }
}