using CSM.Domain.Constants;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

internal sealed record ProjectDataScope(
    Guid CompanyId,
    bool CanAccessAllSites,
    IQueryable<Guid> AccessibleSiteIds);

internal static class ProjectDataAccess
{
    public static async Task<ProjectDataScope> GetScopeAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId && x.IsActive && !x.IsDeleted,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "The authenticated user is unavailable.");

        if (!user.CompanyId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "The authenticated user is not assigned to a company.");
        }

        var canAccessAllSites = user.UserRoles.Any(role =>
            !role.IsDeleted &&
            !role.Role.IsDeleted &&
            (string.Equals(
                 role.Role.Name,
                 AppRoles.SuperAdmin,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 role.Role.Name,
                 AppRoles.CompanyAdmin,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 role.Role.Name,
                 AppRoles.Administrator,
                 StringComparison.OrdinalIgnoreCase)));

        var accessibleSiteIds = context.UserSiteAccesses
            .AsNoTracking()
            .Where(access =>
                access.UserId == userId &&
                access.CompanyId == user.CompanyId.Value &&
                access.IsActive &&
                !access.IsDeleted)
            .Select(access => access.SiteId);

        return new ProjectDataScope(
            user.CompanyId.Value,
            canAccessAllSites,
            accessibleSiteIds);
    }

    public static async Task EnsureSiteAccessAsync(
        ApplicationDbContext context,
        ProjectDataScope scope,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var siteExists = await context.ConstructionSites
            .AsNoTracking()
            .AnyAsync(
                site => site.Id == siteId &&
                        site.CompanyId == scope.CompanyId &&
                        (scope.CanAccessAllSites ||
                         scope.AccessibleSiteIds.Contains(site.Id)),
                cancellationToken);

        if (!siteExists)
        {
            throw new UnauthorizedAccessException(
                "You are not authorized to access this site.");
        }
    }

    public static async Task<Guid> EnsureProjectAccessAsync(
        ApplicationDbContext context,
        ProjectDataScope scope,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var siteId = await context.Projects
            .AsNoTracking()
            .Where(project =>
                project.Id == projectId &&
                project.CompanyId == scope.CompanyId)
            .Select(project => (Guid?)project.ConstructionSiteId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!siteId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "You are not authorized to access this project.");
        }

        await EnsureSiteAccessAsync(
            context,
            scope,
            siteId.Value,
            cancellationToken);

        return siteId.Value;
    }
}
