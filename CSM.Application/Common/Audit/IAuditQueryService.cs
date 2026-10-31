using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSM.Application.Common.Audit;

public interface IAuditQueryService
{
    Task<IReadOnlyCollection<AuditLogDto>> GetLogsAsync(
        Guid actorId,
        string? module,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}