using CSM.Application.Sites.Dtos;

namespace CSM.Application.Sites;

public interface ISiteService
{
    Task<IReadOnlyCollection<SiteResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<SiteResponse> GetByIdAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<SiteResponse> CreateAsync(
        Guid currentUserId,
        CreateSiteRequest request,
        CancellationToken cancellationToken = default);

    Task<SiteResponse> UpdateAsync(
        Guid currentUserId,
        Guid siteId,
        UpdateSiteRequest request,
        CancellationToken cancellationToken = default);

    Task<SiteResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid siteId,
        ChangeSiteStatusRequest request,
        CancellationToken cancellationToken = default);

    Task SetActiveStatusAsync(
        Guid currentUserId,
        Guid siteId,
        bool isActive,
        CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<SiteResponse>> GetAssignmentOptionsAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default);


}