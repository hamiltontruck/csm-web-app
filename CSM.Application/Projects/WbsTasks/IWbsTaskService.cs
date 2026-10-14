using CSM.Application.Projects.WbsTasks.Dtos;

namespace CSM.Application.Projects.WbsTasks;

public interface IWbsTaskService
{
    Task<IReadOnlyCollection<WbsTaskResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<WbsTaskResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<WbsTaskResponse> CreateAsync(
        Guid currentUserId,
        CreateWbsTaskRequest request,
        CancellationToken cancellationToken = default);
}