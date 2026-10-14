using CSM.Application.Projects.Dtos;

namespace CSM.Application.Projects;

public interface IProjectService
{
    Task<IReadOnlyCollection<ProjectResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse> GetByIdAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse> UpdateAsync(
        Guid currentUserId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid projectId,
        ChangeProjectStatusRequest request,
        CancellationToken cancellationToken = default);



}