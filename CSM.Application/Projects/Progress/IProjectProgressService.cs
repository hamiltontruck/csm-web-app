using CSM.Application.Projects.Progress.Dtos;

namespace CSM.Application.Projects.Progress;

public interface IProjectProgressService
{
    Task<ProjectProgressDashboardResponse> GetDashboardAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}