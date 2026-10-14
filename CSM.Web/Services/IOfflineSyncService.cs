using CSM.Web.Models.OfflineSync;

namespace CSM.Web.Services;

public interface IOfflineSyncService
{
    event Action? ConnectionRestored;

    event Action? ConnectionLost;

    Task InitializeConnectionMonitoringAsync();

    Task<bool> IsOnlineAsync();

    Task QueueAsync(
        OfflineSyncOperation operation);

    Task<IReadOnlyList<OfflineSyncOperation>> GetPendingAsync();

    Task RemoveAsync(Guid operationId);
}