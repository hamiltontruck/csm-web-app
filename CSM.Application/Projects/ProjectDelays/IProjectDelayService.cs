using CSM.Application.Projects.ProjectDelays.Dtos;

namespace CSM.Application.Projects.ProjectDelays;

public interface IProjectDelayService
{
    Task<IReadOnlyCollection<ProjectDelayResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid delayId,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectDelayRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse?> UpdateAsync(
        Guid currentUserId,
        Guid delayId,
        UpdateProjectDelayRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid currentUserId,
        Guid delayId,
        CancellationToken cancellationToken = default);
}