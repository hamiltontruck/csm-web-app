using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Documents.Dtos;
using CSM.Domain.Entities.Compliance;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace CSM.Infrastructure.Services;

public sealed class DocumentVersionService : IDocumentVersionService
{
    private readonly ApplicationDbContext _dbContext;

    public DocumentVersionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DocumentVersionResponse>>
        GetByDocumentAsync(
            Guid currentUserId,
            Guid documentId,
            CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync(
            currentUserId,
            documentId,
            cancellationToken);

        return await _dbContext.DocumentVersions
            .AsNoTracking()
            .Where(x => x.DocumentId == document.Id)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new DocumentVersionResponse
            {
                Id = x.Id,
                DocumentId = x.DocumentId,
                VersionNumber = x.VersionNumber,
                FileName = x.FileName,
                StoragePath = x.StoragePath,
                ContentType = x.ContentType,
                FileSizeBytes = x.FileSizeBytes,
                RevisionNotes = x.RevisionNotes,
                IsCurrent = x.IsCurrent,
                UploadedByUserId = x.UploadedByUserId,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentVersionResponse> CreateAsync(
        Guid currentUserId,
        Guid documentId,
        CreateDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync(
            currentUserId,
            documentId,
            cancellationToken);

        var nextVersion = await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == documentId)
            .MaxAsync(
                x => (int?)x.VersionNumber,
                cancellationToken) ?? 0;

        await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == documentId && x.IsCurrent)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.IsCurrent, false),
                cancellationToken);

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = nextVersion + 1,
            FileName = request.FileName,
            StoragePath = request.StoragePath,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            RevisionNotes = request.RevisionNotes,
            UploadedByUserId = currentUserId,
            IsCurrent = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.DocumentVersions.Add(version);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DocumentVersionResponse
        {
            Id = version.Id,
            DocumentId = version.DocumentId,
            VersionNumber = version.VersionNumber,
            FileName = version.FileName,
            StoragePath = version.StoragePath,
            ContentType = version.ContentType,
            FileSizeBytes = version.FileSizeBytes,
            RevisionNotes = version.RevisionNotes,
            IsCurrent = version.IsCurrent,
            UploadedByUserId = version.UploadedByUserId,
            CreatedAtUtc = version.CreatedAtUtc
        };
    }

    public async Task<(Stream Stream, string FileName, string? ContentType)>
        DownloadAsync(
            Guid currentUserId,
            Guid documentId,
            Guid versionId,
            CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync(
            currentUserId,
            documentId,
            cancellationToken);

        var version = await _dbContext.DocumentVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == versionId &&
                    x.DocumentId == document.Id,
                cancellationToken)
            ?? throw new DocumentManagementException(
                "Document version not found.");

        var webRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot");

        var relativePath = version.StoragePath
            .TrimStart('/', '\\')
            .Replace(
                '/',
                Path.DirectorySeparatorChar)
            .Replace(
                '\\',
                Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(
            Path.Combine(webRoot, relativePath));

        var webRootFullPath = Path.GetFullPath(
            webRoot + Path.DirectorySeparatorChar);

        if (!fullPath.StartsWith(
                webRootFullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentManagementException(
                "Invalid document storage path.");
        }

        if (!File.Exists(fullPath))
        {
            throw new DocumentManagementException(
                "Document version file was not found.");
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            useAsync: true);

        return (
            stream,
            version.FileName,
            version.ContentType);
    }

    private async Task<Document> GetDocumentAsync(
        Guid currentUserId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new DocumentManagementException(
                "Current user not found.");

        var document = await _dbContext.Documents
            .SingleOrDefaultAsync(
                x => x.Id == documentId,
                cancellationToken)
            ?? throw new DocumentManagementException(
                "Document not found.");

        if (currentUser.CompanyId != null &&
            document.CompanyId != currentUser.CompanyId)
        {
            throw new DocumentManagementException(
                "You can only access documents in your own company.");
        }

        return document;
    }
}