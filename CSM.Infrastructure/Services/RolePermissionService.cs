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
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await _db.RoleGranularPermissions
            .Where(x => x.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var permission in request.Permissions.Distinct())
        {
            _db.RoleGranularPermissions.Add(
                new RoleGranularPermission
                {
                    RoleId = roleId,
                    Permission = permission
                });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}