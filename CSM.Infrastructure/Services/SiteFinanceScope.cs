using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

internal static class SiteFinanceScope
{
    public static bool IsSiteLimited(User actor)
    {
        var roles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role.Name).ToArray();
        return (roles.Contains("SiteManager", StringComparer.OrdinalIgnoreCase) || roles.Contains("FinanceOfficer", StringComparer.OrdinalIgnoreCase)) &&
            !roles.Any(x => new[] { "SuperAdmin", "CompanyAdmin", "Accountant" }.Contains(x, StringComparer.OrdinalIgnoreCase));
    }

    public static Task<bool> HasSiteAsync(ApplicationDbContext db, User actor, Guid? siteId, CancellationToken cancellationToken)
    {
        if (!siteId.HasValue || !actor.CompanyId.HasValue) return Task.FromResult(false);
        var officer = CSM.Infrastructure.Security.FinanceOfficerScope.IsOfficer(actor);
        return db.ConstructionSites.AsNoTracking().AnyAsync(s => s.Id == siteId.Value && s.CompanyId == actor.CompanyId && !s.IsDeleted && (!officer || s.IsActive) &&
            db.UserSiteAccesses.Any(a => a.UserId == actor.Id && a.SiteId == s.Id && a.CompanyId == s.CompanyId && a.IsActive && !a.IsDeleted), cancellationToken);
    }
    public static async Task EnsureOfficerReferencesAsync(ApplicationDbContext db, User actor, Guid? siteId,
        Guid? projectId, Guid? costCodeId, CancellationToken cancellationToken, Func<string, Exception> error)
    {
        if (!CSM.Infrastructure.Security.FinanceOfficerScope.IsOfficer(actor)) return;
        if (!await HasSiteAsync(db, actor, siteId, cancellationToken))
            throw error("The selected site is unavailable or outside your assigned scope.");
        if (projectId.HasValue && !await db.Projects.AnyAsync(x => x.Id == projectId && x.CompanyId == actor.CompanyId && x.ConstructionSiteId == siteId && !x.IsDeleted, cancellationToken))
            throw error("The selected project does not belong to this site.");
        if (costCodeId.HasValue && !await db.CostCodes.AnyAsync(x => x.Id == costCodeId && x.CompanyId == actor.CompanyId && !x.IsDeleted && (x.ConstructionSiteId == null || x.ConstructionSiteId == siteId), cancellationToken))
            throw error("The selected cost code does not belong to this site or its shared company catalog.");
    }

}
