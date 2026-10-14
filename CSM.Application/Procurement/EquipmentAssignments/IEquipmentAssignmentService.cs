using CSM.Application.Procurement.EquipmentAssignments.Dtos;

namespace CSM.Application.Procurement.EquipmentAssignments;

public interface IEquipmentAssignmentService
{
    Task<IReadOnlyCollection<EquipmentAssignmentResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? equipmentId = null,
        Guid? constructionSiteId = null,
        Guid? projectId = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<EquipmentAssignmentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<EquipmentAssignmentResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentAssignmentResponse> UpdateAsync(
        Guid currentUserId,
        Guid assignmentId,
        UpdateEquipmentAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentAssignmentResponse> ReleaseAsync(
        Guid currentUserId,
        Guid assignmentId,
        DateTime releasedAtUtc,
        decimal? meterReadingAtRelease,
        CancellationToken cancellationToken = default);
}
