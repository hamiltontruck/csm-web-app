namespace CSM.Web.Services;

public interface IOfflineSyncCoordinator
{
    Task SyncAsync(
        CancellationToken cancellationToken = default);
}