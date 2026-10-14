using CSM.Application.Features.Compliance.SafetyObservations.DTOs;

namespace CSM.Application.Features.Compliance.SafetyObservations.Services;

public interface ISafetyObservationService
{
    Task<IReadOnlyList<SafetyObservationListItem>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<SafetyObservationResponse> GetByIdAsync(
        Guid currentUserId,
        Guid observationId,
        CancellationToken cancellationToken = default);

    Task<SafetyObservationResponse> CreateAsync(
        Guid currentUserId,
        CreateSafetyObservationRequest request,
        CancellationToken cancellationToken = default);

    Task<SafetyObservationResponse> UpdateAsync(
        Guid currentUserId,
        Guid observationId,
        UpdateSafetyObservationRequest request,
        CancellationToken cancellationToken = default);
}