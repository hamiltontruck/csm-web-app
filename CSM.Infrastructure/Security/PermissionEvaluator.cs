using CSM.Domain.Enums;
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

        return await _db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Role.GranularPermissions)
            .AnyAsync(
                x => x.Permission == parsedPermission,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Role.GranularPermissions)
            .Select(x => x.Permission.ToString())
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }
}