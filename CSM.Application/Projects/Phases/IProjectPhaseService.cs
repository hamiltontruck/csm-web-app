using CSM.Application.Projects.Phases.Dtos;

namespace CSM.Application.Projects.Phases;

public interface IProjectPhaseService
{
    Task<IReadOnlyCollection<ProjectPhaseResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectPhaseResponse> GetByIdAsync(
        Guid currentUserId,
        Guid phaseId,
        CancellationToken cancellationToken = default);

    Task<ProjectPhaseResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectPhaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectPhaseResponse> UpdateAsync(
        Guid currentUserId,
        Guid phaseId,
        UpdateProjectPhaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectPhaseResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid phaseId,
        ChangeProjectPhaseStatusRequest request,
        CancellationToken cancellationToken = default);
}