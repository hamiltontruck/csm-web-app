using CSM.Application.Compliance.Documents.Dtos;




namespace CSM.Application.Compliance.Documents;

public interface IDocumentService
{
    Task<IReadOnlyCollection<DocumentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<DocumentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DocumentResponse> CreateAsync(
        Guid currentUserId,
        CreateDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentResponse> UpdateAsync(
        Guid currentUserId,
        Guid documentId,
        UpdateDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentVersionResponse> UploadVersionAsync(
        Guid currentUserId,
        Guid documentId,
        Stream fileStream,
        string fileName,
        string? contentType,
        long fileSizeBytes,
        string? revisionNotes,
        CancellationToken cancellationToken = default);
}
