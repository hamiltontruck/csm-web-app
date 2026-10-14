using System;
using System.Threading;
using System.Threading.Tasks;

namespace CSM.Application.Common.Audit;

public interface IAuditService
{
    Task LogAsync(
        Guid userId,
        string userName,
        string module,
        string action,
        string entityName,
        string entityId,
        string description,
        string? ipAddress,
        CancellationToken cancellationToken);
}