using CSM.Application.Compliance.Documents.Dtos;

namespace CSM.Application.Compliance.Documents;

public interface IDocumentVersionService
{
    Task<IReadOnlyCollection<DocumentVersionResponse>> GetByDocumentAsync(
        Guid currentUserId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<DocumentVersionResponse> CreateAsync(
        Guid currentUserId,
        Guid documentId,
        CreateDocumentVersionRequest request,
        CancellationToken cancellationToken);

    Task<(Stream Stream, string FileName, string? ContentType)> DownloadAsync(
        Guid currentUserId,
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken);
}