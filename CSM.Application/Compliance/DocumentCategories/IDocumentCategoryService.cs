using CSM.Application.Compliance.DocumentCategories.Dtos;

namespace CSM.Application.Compliance.DocumentCategories;

public interface IDocumentCategoryService
{
    Task<IReadOnlyCollection<DocumentCategoryResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<DocumentCategoryResponse> GetByIdAsync(
        Guid currentUserId,
        Guid documentCategoryId,
        CancellationToken cancellationToken = default);

    Task<DocumentCategoryResponse> CreateAsync(
        Guid currentUserId,
        CreateDocumentCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentCategoryResponse> UpdateAsync(
        Guid currentUserId,
        Guid documentCategoryId,
        UpdateDocumentCategoryRequest request,
        CancellationToken cancellationToken = default);
}
