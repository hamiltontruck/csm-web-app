using CSM.Application.Procurement.Materials.Dtos;

namespace CSM.Application.Procurement.Materials;

public interface IMaterialService
{
    Task<IReadOnlyCollection<MaterialResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    Task<MaterialResponse> GetByIdAsync(
        Guid currentUserId,
        Guid materialId,
        CancellationToken cancellationToken = default);

    Task<MaterialResponse> CreateAsync(
        Guid currentUserId,
        CreateMaterialRequest request,
        CancellationToken cancellationToken = default);

    Task<MaterialResponse> UpdateAsync(
        Guid currentUserId,
        Guid materialId,
        UpdateMaterialRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaterialLookupDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaterialLookupDto>> GetUnitsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaterialLookupDto>> GetMaterialTypesAsync(
        CancellationToken cancellationToken = default);
}
