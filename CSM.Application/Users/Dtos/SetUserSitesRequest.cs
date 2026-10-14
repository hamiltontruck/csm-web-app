namespace CSM.Application.Users.Dtos;

public sealed record SetUserSitesRequest(
    Guid? DefaultSiteId,
    IReadOnlyCollection<Guid> SiteIds
);