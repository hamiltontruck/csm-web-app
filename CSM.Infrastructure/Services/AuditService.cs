using CSM.Application.Common.Audit;
using CSM.Domain.Entities.Audit;
using CSM.Infrastructure.Persistence;

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
        var log = new AuditLog
        {
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