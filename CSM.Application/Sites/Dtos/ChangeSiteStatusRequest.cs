using CSM.Domain.Enums;

namespace CSM.Application.Sites.Dtos;

public sealed record ChangeSiteStatusRequest(
    SiteStatus Status);