using CSM.Application.HRM.SiteAssignments.Dtos;

namespace CSM.Application.HRM.SiteAssignments;

public interface ISiteAssignmentService
{
    Task<IReadOnlyCollection<SiteAssignmentResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        Guid? constructionSiteId = null,
        CancellationToken cancellationToken = default);

    Task<SiteAssignmentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<SiteAssignmentResponse> CreateAsync(
        Guid currentUserId,
        CreateSiteAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<SiteAssignmentResponse> UpdateAsync(
        Guid currentUserId,
        Guid assignmentId,
        UpdateSiteAssignmentRequest request,
        CancellationToken cancellationToken = default);
}