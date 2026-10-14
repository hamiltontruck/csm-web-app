using CSM.Application.Projects.Tasks.Dtos;

namespace CSM.Application.Projects.Tasks;

public interface IWorkTaskService
{
    Task<IReadOnlyCollection<WorkTaskResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        Guid? projectPhaseId = null,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> GetByIdAsync(
        Guid currentUserId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> CreateAsync(
        Guid currentUserId,
        CreateWorkTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> UpdateAsync(
        Guid currentUserId,
        Guid taskId,
        UpdateWorkTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid taskId,
        ChangeWorkTaskStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> UpdateProgressAsync(
        Guid currentUserId,
        Guid taskId,
        UpdateWorkTaskProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> AddDependencyAsync(
        Guid currentUserId,
        Guid taskId,
        AddWorkTaskDependencyRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkTaskResponse> RemoveDependencyAsync(
        Guid currentUserId,
        Guid taskId,
        Guid dependsOnTaskId,
        CancellationToken cancellationToken = default);
}