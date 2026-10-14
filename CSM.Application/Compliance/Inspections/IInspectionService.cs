using CSM.Application.Compliance.Inspections.Dtos;

namespace CSM.Application.Compliance.Inspections;

public interface IInspectionService
{
    Task<IReadOnlyCollection<InspectionResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<InspectionResponse> GetByIdAsync(
        Guid currentUserId,
        Guid inspectionId,
        CancellationToken cancellationToken = default);

    Task<InspectionResponse> CreateAsync(
        Guid currentUserId,
        CreateInspectionRequest request,
        CancellationToken cancellationToken = default);

    Task<InspectionResponse> UpdateAsync(
        Guid currentUserId,
        Guid inspectionId,
        UpdateInspectionRequest request,
        CancellationToken cancellationToken = default);
}
