using CSM.Web.Models.HRM;

namespace CSM.Web.Services.HRM;

public interface ISiteAssignmentService
{
    Task<IReadOnlyCollection<SiteAssignmentResponse>> GetAllAsync(
        Guid? employeeId = null,
        Guid? constructionSiteId = null);

    Task<SiteAssignmentResponse?> GetByIdAsync(Guid assignmentId);

    Task<SiteAssignmentResponse?> CreateAsync(
        CreateSiteAssignmentRequest request);

    Task<SiteAssignmentResponse?> UpdateAsync(
        Guid assignmentId,
        UpdateSiteAssignmentRequest request);
}