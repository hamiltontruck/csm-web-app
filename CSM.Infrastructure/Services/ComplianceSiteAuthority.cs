using CSM.Domain.Common;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace CSM.Infrastructure.Services;

internal static class ComplianceSiteAuthority
{
    internal static bool HasRole(User actor, string role) => actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted && string.Equals(x.Role.Name, role, StringComparison.OrdinalIgnoreCase));
    internal static async Task<User> ActorAsync(ApplicationDbContext db, Guid actorId, CancellationToken ct)
    {
        var actor = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, ct);
        if (actor is null || (!HasRole(actor, "SuperAdmin") && (!actor.CompanyId.HasValue || actor.CompanyId == Guid.Empty)) ||
            !await new PermissionEvaluator(db).HasPermissionAsync(actorId, "Compliance_View", ct))
            throw new UnauthorizedAccessException("Compliance access is not permitted.");
        return actor;
    }
    internal static async Task ManageAsync(ApplicationDbContext db, User actor, CancellationToken ct)
    {
        if (!await new PermissionEvaluator(db).HasPermissionAsync(actor.Id, "Compliance_Manage", ct))
            throw new UnauthorizedAccessException("Compliance management is not permitted.");
    }
    internal static IQueryable<ConstructionSite> Sites(ApplicationDbContext db, User actor)
    {
        var sites = db.ConstructionSites.AsNoTracking().Where(x => !x.IsDeleted);
        if (HasRole(actor, "SuperAdmin")) return sites;
        var company = actor.CompanyId!.Value;
        sites = sites.Where(x => x.CompanyId == company);
        if (HasRole(actor, "CompanyAdmin")) return sites;
        var actorId = actor.Id;
        return sites.Where(x => db.UserSiteAccesses.Any(a => a.UserId == actorId && a.CompanyId == company && a.SiteId == x.Id && a.IsActive && !a.IsDeleted));
    }
    internal static IQueryable<T> Records<T>(IQueryable<T> rows, ApplicationDbContext db, User actor) where T : TenantEntity
    {
        var siteIds = Sites(db, actor).Select(x => x.Id);
        rows = rows.Where(x => !x.IsDeleted && siteIds.Contains(EF.Property<Guid>(x, "ConstructionSiteId")) &&
            db.ConstructionSites.Any(s => s.Id == EF.Property<Guid>(x, "ConstructionSiteId") && s.CompanyId == x.CompanyId));
        if (!HasRole(actor, "SuperAdmin"))
        {
            var company = actor.CompanyId!.Value;
            rows = rows.Where(x => x.CompanyId == company);
        }
        return rows;
    }
    internal static async Task<Guid> CreationCompanyAsync(ApplicationDbContext db, User actor, Guid siteId, CancellationToken ct)
    {
        var company = await Sites(db, actor).Where(x => x.Id == siteId).Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(ct);
        return company ?? throw new KeyNotFoundException("Construction site was not found.");
    }
}
