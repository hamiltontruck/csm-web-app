using CSM.Application.Projects.ProjectMilestones.Dtos;

namespace CSM.Application.Projects.ProjectMilestones;

public interface IProjectMilestoneService
{
    Task<IReadOnlyCollection<ProjectMilestoneResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default);
    Task<ProjectMilestoneResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default);

    Task<ProjectMilestoneResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectMilestoneRequest request,
        CancellationToken cancellationToken = default);
}