using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

/// <summary>Resolve live authority for one action. The returned actor is untracked;
/// ungranted global roles cannot widen a FinanceOfficer action.</summary>
internal static class FinanceOfficerScope
{
    public static bool IsOfficer(User actor) => actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted &&
        string.Equals(x.Role.Name, AppRoles.FinanceOfficer, StringComparison.OrdinalIgnoreCase));

    public static async Task<User?> ResolveActorAsync(ApplicationDbContext db, Guid actorId,
        GranularPermission action, CancellationToken ct, Func<string, Exception> error)
    {
        var actor = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == actorId, ct);
        if (actor is null) return null;
        var knownOfficer = actor.UserRoles.Any(x => string.Equals(x.Role.Name, AppRoles.FinanceOfficer, StringComparison.OrdinalIgnoreCase));
        if (!knownOfficer) return null;
        if (!actor.IsActive || actor.IsDeleted) throw error("Current user is inactive or unavailable.");

        var live = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).ToArray();
        bool Grants(UserRole link) => RolePermissionRules.Allows(link.Role.Name, action.ToString()) &&
            link.Role.GranularPermissions.Any(p => p.Permission == action);
        var independent = live.Where(x => !string.Equals(x.Role.Name, AppRoles.FinanceOfficer, StringComparison.OrdinalIgnoreCase) && Grants(x)).ToArray();
        if (independent.Length > 0)
        {
            actor.UserRoles = independent;
            return actor;
        }
        var officer = live.Where(x => string.Equals(x.Role.Name, AppRoles.FinanceOfficer, StringComparison.OrdinalIgnoreCase) && Grants(x)).ToArray();
        if (officer.Length == 0 || actor.CompanyId is null || actor.CompanyId == Guid.Empty)
            throw error("This action is not permitted for your account.");
        actor.UserRoles = officer;
        return actor;
    }
}
