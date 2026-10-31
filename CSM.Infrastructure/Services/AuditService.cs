using CSM.Application.Common.Audit;
using CSM.Domain.Entities.Audit;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid userId,
        string userName,
        string module,
        string action,
        string entityName,
        string entityId,
        string description,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        // Only a live database actor can supply the tenant for a new log. Never
        // infer the tenant of existing logs from a user’s present-day company.
        var companyId = await _context.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive && !x.IsDeleted)
            .Select(x => x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        var log = new AuditLog
        {
            CompanyId = companyId ?? Guid.Empty,
            UserId = userId,
            UserName = userName,
            Module = module,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            IpAddress = ipAddress,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);

        await _context.SaveChangesAsync(cancellationToken);
    }
}