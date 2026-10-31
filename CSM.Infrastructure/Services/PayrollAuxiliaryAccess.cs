using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

internal static class PayrollAuxiliaryAccess
{
    internal static async Task<Employee> GetEmployeeAsync(
        ApplicationDbContext db,
        Guid currentUserId,
        Guid employeeId,
        bool manage,
        CancellationToken cancellationToken)
    {
        // Read current database state, never token role/company claims or tracked role snapshots.
        var actor = await db.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .FirstOrDefaultAsync(x => x.Id == currentUserId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Payroll access denied.");

        var roles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role).ToArray();
        bool HasRole(string name) => roles.Any(x => string.Equals(x.Name, name, StringComparison.Ordinal));
        var global = HasRole(AppRoles.SuperAdmin);
        var manager = global || HasRole(AppRoles.CompanyAdmin) || HasRole(AppRoles.HrOfficer) || HasRole(AppRoles.HROfficer);
        if ((!manager && (manage || !HasRole(AppRoles.Administrator))) ||
            !roles.SelectMany(x => x.GranularPermissions).Any(x => x.Permission == GranularPermission.HRM_Payroll_View) ||
            (!global && (!actor.CompanyId.HasValue || actor.CompanyId == Guid.Empty)))
        {
            throw new UnauthorizedAccessException("Payroll access denied.");
        }

        // Validate the employee parent before querying salary data or staging any salary write.
        return await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == employeeId && !x.IsDeleted &&
                (global || x.CompanyId == actor.CompanyId), cancellationToken)
            ?? throw new KeyNotFoundException("Employee was not found.");
    }
}
