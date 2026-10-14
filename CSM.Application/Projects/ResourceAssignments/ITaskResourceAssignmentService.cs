using CSM.Application.Projects.ResourceAssignments.Dtos;

namespace CSM.Application.Projects.ResourceAssignments;

public interface ITaskResourceAssignmentService
{
    Task<IReadOnlyList<TaskResourceAssignmentResponse>> GetByTaskAsync(
        Guid wbsTaskId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<TaskResourceAssignmentResponse> CreateAsync(
        CreateTaskResourceAssignmentRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<TaskResourceAssignmentResponse> UpdateAsync(
        Guid id,
        UpdateTaskResourceAssignmentRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}