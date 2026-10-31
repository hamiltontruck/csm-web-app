using CSM.Application.Roles;
using CSM.Application.Roles.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class RolePermissionService : IRolePermissionService
{
    private readonly ApplicationDbContext _db;

    public RolePermissionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<RoleSummaryDto>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Roles
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new RoleSummaryDto(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RolePermissionDto>> GetPermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var granted = await _db.RoleGranularPermissions
            .Where(x => x.RoleId == roleId)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);

        return Enum.GetValues<GranularPermission>()
            .Select(p => new RolePermissionDto(
                p,
                granted.Contains(p)))
            .ToList();
    }

    public async Task UpdatePermissionsAsync(
        Guid actorId,
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        // Roles are global, not tenant-owned. A company administrator may read
        // the matrix but must never change authority for users in other companies.
        var actor = await _db.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken);
        if (actor is null || !actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted && x.Role.Name == "SuperAdmin") ||
            !await new CSM.Infrastructure.Security.PermissionEvaluator(_db).HasPermissionAsync(actorId, "Users_Edit", cancellationToken))
            throw new UnauthorizedAccessException("You are not authorized to change shared role permissions.");
        if (request.Permissions is null || request.Permissions.Any(p => !Enum.IsDefined(p)))
            throw new ArgumentException("One or more permissions are invalid.");
        if (!await _db.Roles.AnyAsync(x => x.Id == roleId && !x.IsDeleted, cancellationToken))
            throw new KeyNotFoundException("Role was not found.");

        var selected = request.Permissions.ToHashSet();
        var existing = await _db.RoleGranularPermissions.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);
        _db.RoleGranularPermissions.RemoveRange(existing.Where(x => !selected.Contains(x.Permission)));
        foreach (var permission in selected.Except(existing.Select(x => x.Permission)))
            _db.RoleGranularPermissions.Add(new RoleGranularPermission { RoleId = roleId, Permission = permission });
        // One SaveChanges transaction: validation or insertion failure cannot erase old grants.
        await _db.SaveChangesAsync(cancellationToken);
    }
}
