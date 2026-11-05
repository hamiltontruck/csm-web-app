using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

/// <summary>Live corporate Accountant grants for reads and the explicit Purchase Order exception.</summary>
internal static class AccountantReadAccess
{
    internal static Task<bool> HasAsync(ApplicationDbContext db, Guid actorId,
        GranularPermission permission, CancellationToken cancellationToken) =>
        permission.ToString().EndsWith("_View", StringComparison.Ordinal) &&
        AccountantPermissionRules.Allows(AppRoles.Accountant, permission.ToString())
            ? HasGrantAsync(db, actorId, permission, cancellationToken) : Task.FromResult(false);

    internal static Task<bool> HasPurchaseOrderPermissionAsync(ApplicationDbContext db, Guid actorId,
        GranularPermission permission, CancellationToken cancellationToken) =>
        permission is GranularPermission.Procurement_PurchaseOrder_Manage or GranularPermission.Procurement_PurchaseOrder_Approve
            ? HasGrantAsync(db, actorId, permission, cancellationToken) : Task.FromResult(false);

    private static Task<bool> HasGrantAsync(ApplicationDbContext db, Guid actorId,
        GranularPermission permission, CancellationToken cancellationToken) =>
        db.UserRoles.AsNoTracking().AnyAsync(x =>
            x.UserId == actorId && !x.IsDeleted && !x.Role.IsDeleted &&
            x.Role.Name == AppRoles.Accountant && x.User.IsActive && !x.User.IsDeleted &&
            x.User.CompanyId.HasValue && x.User.CompanyId != Guid.Empty &&
            x.Role.GranularPermissions.Any(p => p.Permission == permission), cancellationToken);
}
