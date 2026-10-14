using CSM.Application.Projects.ProjectDelays.Dtos;

namespace CSM.Application.Projects.ProjectDelays;

public interface IProjectDelayService
{
    Task<IReadOnlyCollection<ProjectDelayResponse>> GetAllAsync(
        Guid tenantId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse?> GetByIdAsync(
        Guid tenantId,
        Guid delayId,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse> CreateAsync(
        Guid tenantId,
        CreateProjectDelayRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse?> UpdateAsync(
        Guid tenantId,
        Guid delayId,
        UpdateProjectDelayRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid tenantId,
        Guid delayId,
        CancellationToken cancellationToken = default);
}