using CSM.Domain.Enums;
using CSM.Domain.Constants;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

public sealed class PermissionEvaluator : IPermissionEvaluator
{
    private readonly ApplicationDbContext _db;

    public PermissionEvaluator(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<GranularPermission>(
                permission,
                ignoreCase: false,
                out var parsedPermission))
        {
            return false;
        }

        var officerAllowed = RolePermissionRules.Allows(AppRoles.ProcurementOfficer, parsedPermission.ToString());
        var financeAllowed = FinanceOfficerPermissionRules.Allows(AppRoles.FinanceOfficer, parsedPermission.ToString());
        var accountantAllowed = AccountantPermissionRules.Allows(AppRoles.Accountant, parsedPermission.ToString());
        return await _db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted &&
                x.User.IsActive && !x.User.IsDeleted && !x.Role.IsDeleted)
            .Where(x => x.Role.Name.ToUpper() != "FINANCEOFFICER" || financeAllowed)
            .Where(x => x.Role.Name != AppRoles.Accountant || accountantAllowed)
            .Where(x => x.Role.Name != AppRoles.ProcurementOfficer || officerAllowed)
            .SelectMany(x => x.Role.GranularPermissions)
            .AnyAsync(
                x => x.Permission == parsedPermission,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var grants = await _db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted &&
                x.User.IsActive && !x.User.IsDeleted && !x.Role.IsDeleted)
            .SelectMany(x => x.Role.GranularPermissions,
                (membership, grant) => new { RoleName = membership.Role.Name, grant.Permission })
            .ToListAsync(cancellationToken);
        return grants.Where(x => RolePermissionRules.Allows(x.RoleName, x.Permission.ToString()))
            .Select(x => x.Permission.ToString()).Distinct().OrderBy(x => x).ToList();
    }
}

