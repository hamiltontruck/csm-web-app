using CSM.Application.Projects.Milestones.Dtos;

namespace CSM.Application.Projects.Milestones;

public interface IMilestoneService
{
    Task<IReadOnlyCollection<MilestoneResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        Guid? projectPhaseId = null,
        CancellationToken cancellationToken = default);

    Task<MilestoneResponse> GetByIdAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default);

    Task<MilestoneResponse> CreateAsync(
        Guid currentUserId,
        CreateMilestoneRequest request,
        CancellationToken cancellationToken = default);

    Task<MilestoneResponse> UpdateAsync(
        Guid currentUserId,
        Guid milestoneId,
        UpdateMilestoneRequest request,
        CancellationToken cancellationToken = default);

    Task<MilestoneResponse> CompleteAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default);

    Task<MilestoneResponse> ReopenAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default);
}