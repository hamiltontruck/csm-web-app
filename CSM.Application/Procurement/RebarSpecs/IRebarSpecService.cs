using CSM.Application.Procurement.RebarSpecs.Dtos;

namespace CSM.Application.Procurement.RebarSpecs;

public interface IRebarSpecService
{
    Task<RebarSpecResponse> GetByMaterialIdAsync(
        Guid currentUserId,
        Guid materialId,
        CancellationToken cancellationToken = default);

    Task<RebarSpecResponse> CreateAsync(
        Guid currentUserId,
        CreateRebarSpecRequest request,
        CancellationToken cancellationToken = default);

    Task<RebarSpecResponse> UpdateAsync(
        Guid currentUserId,
        Guid materialId,
        UpdateRebarSpecRequest request,
        CancellationToken cancellationToken = default);
}
