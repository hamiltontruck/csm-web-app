using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Documents.Dtos;
using CSM.Domain.Entities.Compliance;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;


namespace CSM.Infrastructure.Services;

public sealed class DocumentVersionService : IDocumentVersionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentService _documents;
    private readonly DocumentFileStorage _files;

    public DocumentVersionService(ApplicationDbContext dbContext, IDocumentService documents, IWebHostEnvironment? environment = null)
    {
        _dbContext = dbContext;
        _documents = documents;
        _files = new(environment);
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
        await DocumentService.RequireAccessAsync(_dbContext, currentUserId, true, cancellationToken);
        var document = await GetDocumentAsync(
            currentUserId,
            documentId,
            cancellationToken);

        var existing = await _dbContext.DocumentVersions.AsNoTracking().Where(x => x.DocumentId == document.Id).ToListAsync(cancellationToken);
        var registered = existing.FirstOrDefault(x => _files.SameFile(document, x.StoragePath, request.StoragePath))
            ?? throw new DocumentManagementException("Upload a file or reuse a file already registered to this document.");
        await _files.OwnedFileAsync(_dbContext, document, registered.StoragePath, cancellationToken);

        var nextVersion = await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == documentId)
            .MaxAsync(
                x => (int?)x.VersionNumber,
                cancellationToken) ?? 0;

        var currentVersions = await _dbContext.DocumentVersions.Where(x => x.DocumentId == documentId && x.IsCurrent).ToListAsync(cancellationToken);
        foreach (var current in currentVersions) current.IsCurrent = false;

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = nextVersion + 1,
            FileName = registered.FileName,
            StoragePath = registered.StoragePath,
            ContentType = registered.ContentType,
            FileSizeBytes = registered.FileSizeBytes,
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

        var fullPath = await _files.OwnedFileAsync(_dbContext, document, version.StoragePath, cancellationToken);

        var stream = DocumentFileStorage.OpenRead(fullPath);

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
        await DocumentService.RequireAccessAsync(_dbContext, currentUserId, false, cancellationToken);
        // Reuse document authorization before reading versions, changing metadata, or opening a file.
        await _documents.GetByIdAsync(currentUserId, documentId, cancellationToken);

        var document = await _dbContext.Documents
            .SingleOrDefaultAsync(
                x => x.Id == documentId && !x.IsDeleted,
                cancellationToken)
            ?? throw new DocumentManagementException("Document not found.");

        return document;
    }
}