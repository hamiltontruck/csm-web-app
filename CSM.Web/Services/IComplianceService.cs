using CSM.Web.Models.Compliance;

namespace CSM.Web.Services;

public interface IComplianceService
{
    Task<IReadOnlyList<DocumentCategoryResponse>> GetDocumentCategoriesAsync(
        bool? isActive = null);

    Task<DocumentCategoryResponse?> GetDocumentCategoryAsync(
        Guid id);

    Task<DocumentCategoryResponse> CreateDocumentCategoryAsync(
        CreateDocumentCategoryRequest request);

    Task<DocumentCategoryResponse> UpdateDocumentCategoryAsync(
        Guid id,
        UpdateDocumentCategoryRequest request);

    Task<IReadOnlyList<DocumentResponse>> GetDocumentsAsync();

    Task<DocumentResponse?> GetDocumentAsync(Guid id);

    Task<DocumentResponse> CreateDocumentAsync(
        CreateDocumentRequest request);

    Task<DocumentResponse> UpdateDocumentAsync(
        Guid id,
        UpdateDocumentRequest request);

    Task<DocumentVersionResponse> UploadDocumentVersionAsync(
        Guid documentId,
        Microsoft.AspNetCore.Components.Forms.IBrowserFile file,
        string? revisionNotes);

    Task<IReadOnlyList<DocumentVersionResponse>> GetDocumentVersionsAsync(
        Guid documentId);

    Task<(byte[] Content, string FileName, string ContentType)>
        DownloadDocumentVersionAsync(
            Guid documentId,
            Guid versionId);

    Task<IReadOnlyList<SafetyIncidentResponse>> GetSafetyIncidentsAsync();

    Task<SafetyIncidentResponse?> GetSafetyIncidentAsync(Guid id);

    Task<SafetyIncidentResponse> CreateSafetyIncidentAsync(
        CreateSafetyIncidentRequest request);

    Task<SafetyIncidentResponse> UpdateSafetyIncidentAsync(
        Guid id,
        UpdateSafetyIncidentRequest request);
}