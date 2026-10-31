using CSM.Application.Common.Audit;
using CSM.Domain.Constants;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly ApplicationDbContext _context;

    public AuditQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<AuditLogDto>> GetLogsAsync(
        Guid actorId,
        string? module,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var actor = await _context.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken);
        var roles = actor?.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted)
            .Select(x => x.Role.Name).ToArray() ?? [];
        var isSuperAdmin = roles.Contains(AppRoles.SuperAdmin);
        if (actor is null ||
            !roles.Any(x => x == AppRoles.SuperAdmin || x == AppRoles.CompanyAdmin || x == AppRoles.Administrator) ||
            !await new PermissionEvaluator(_context).HasPermissionAsync(actorId, "Users_View", cancellationToken) ||
            (!isSuperAdmin && (!actor.CompanyId.HasValue || actor.CompanyId == Guid.Empty)))
        {
            throw new UnauthorizedAccessException("You are not authorized to read audit logs.");
        }

        var query = _context.AuditLogs.AsNoTracking().Where(x => !x.IsDeleted);
        if (!isSuperAdmin)
        {
            // Unknown historical/system tenants cannot be attributed to an actor’s current company.
            var companyId = actor.CompanyId!.Value;
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(x => x.Module == module);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserName = x.UserName,
                Module = x.Module,
                Action = x.Action,
                EntityName = x.EntityName,
                Description = x.Description,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}