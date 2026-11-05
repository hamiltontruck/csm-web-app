using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

/// <summary>Adds corporate Accountant reads without expanding existing mutation role gates.</summary>
internal static class AccountantReadAccess
{
    internal static async Task<bool> HasAsync(ApplicationDbContext db, Guid actorId,
        GranularPermission permission, CancellationToken cancellationToken)
    {
        if (!permission.ToString().EndsWith("_View", StringComparison.Ordinal)) return false;
        return await db.UserRoles.AsNoTracking().AnyAsync(x =>
            x.UserId == actorId && !x.IsDeleted && !x.Role.IsDeleted &&
            x.Role.Name == AppRoles.Accountant && x.User.IsActive && !x.User.IsDeleted &&
            x.User.CompanyId.HasValue && x.User.CompanyId != Guid.Empty &&
            x.Role.GranularPermissions.Any(p => p.Permission == permission), cancellationToken);
    }
}
