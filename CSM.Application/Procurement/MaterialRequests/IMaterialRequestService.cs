using CSM.Application.Procurement.MaterialRequests.Dtos;
using CSM.Domain.Enums;

namespace CSM.Application.Procurement.MaterialRequests;

public interface IMaterialRequestService
{
    Task<IReadOnlyCollection<MaterialRequestResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? projectId = null,
        MaterialRequestStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> GetByIdAsync(
        Guid currentUserId,
        Guid materialRequestId,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> CreateAsync(
        Guid currentUserId,
        CreateMaterialRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> UpdateAsync(
        Guid currentUserId,
        Guid materialRequestId,
        UpdateMaterialRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> SubmitAsync(
        Guid currentUserId,
        Guid materialRequestId,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> ApproveAsync(
        Guid currentUserId,
        Guid materialRequestId,
        ApproveMaterialRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> RejectAsync(
        Guid currentUserId,
        Guid materialRequestId,
        MaterialRequestRemarksRequest request,
        CancellationToken cancellationToken = default);

    Task<MaterialRequestResponse> CancelAsync(
        Guid currentUserId,
        Guid materialRequestId,
        MaterialRequestRemarksRequest request,
        CancellationToken cancellationToken = default);
}
