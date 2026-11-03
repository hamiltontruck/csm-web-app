using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

internal static class SiteFinanceScope
{
    public static bool IsSiteLimited(User actor)
    {
        var roles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role.Name).ToArray();
        return roles.Contains("SiteManager", StringComparer.OrdinalIgnoreCase) &&
            !roles.Any(x => new[] { "SuperAdmin", "CompanyAdmin", "Accountant" }.Contains(x, StringComparer.OrdinalIgnoreCase));
    }

    public static Task<bool> HasSiteAsync(ApplicationDbContext db, User actor, Guid? siteId, CancellationToken cancellationToken)
    {
        if (!siteId.HasValue || !actor.CompanyId.HasValue) return Task.FromResult(false);
        return db.ConstructionSites.AsNoTracking().AnyAsync(s => s.Id == siteId.Value && s.CompanyId == actor.CompanyId && !s.IsDeleted &&
            db.UserSiteAccesses.Any(a => a.UserId == actor.Id && a.SiteId == s.Id && a.CompanyId == s.CompanyId && a.IsActive && !a.IsDeleted), cancellationToken);
    }
}
