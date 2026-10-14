using CSM.Web.Models.OfflineSync;

namespace CSM.Web.Services;

public interface IOfflineSyncApi
{
    Task ExecuteAsync(
        OfflineSyncOperation operation,
        CancellationToken cancellationToken = default);
}