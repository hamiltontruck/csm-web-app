using CSM.Application.Common.Exceptions;
using CSM.Application.Users;
using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class UserRoleOptionsService(ApplicationDbContext db) : IUserRoleOptionsService
{
    public async Task<IReadOnlyCollection<string>> GetAssignableRolesAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .ThenInclude(x => x.GranularPermissions).SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken);
        var live = actor?.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).ToArray() ?? [];
        var manager = live.Any(x => x.Role.Name is AppRoles.SuperAdmin or AppRoles.CompanyAdmin);
        var permitted = live.Any(x => x.Role.GranularPermissions.Any(g =>
            (g.Permission is GranularPermission.Users_Create or GranularPermission.Users_Edit) && RolePermissionRules.Allows(x.Role.Name, g.Permission.ToString())));
        if (!manager || !permitted) throw new UserManagementException("You are not authorized to manage user roles.");
        return await db.Roles.AsNoTracking().Where(x => !x.IsDeleted && x.Name != AppRoles.SuperAdmin)
            .OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(cancellationToken);
    }
}
