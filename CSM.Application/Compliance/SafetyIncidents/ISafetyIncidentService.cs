using CSM.Application.Compliance.SafetyIncidents.Dtos;

namespace CSM.Application.Compliance.SafetyIncidents;

public interface ISafetyIncidentService
{
    Task<IReadOnlyCollection<SafetyIncidentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<SafetyIncidentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid safetyIncidentId,
        CancellationToken cancellationToken = default);

    Task<SafetyIncidentResponse> CreateAsync(
        Guid currentUserId,
        CreateSafetyIncidentRequest request,
        CancellationToken cancellationToken = default);

    Task<SafetyIncidentResponse> UpdateAsync(
        Guid currentUserId,
        Guid safetyIncidentId,
        UpdateSafetyIncidentRequest request,
        CancellationToken cancellationToken = default);
}
