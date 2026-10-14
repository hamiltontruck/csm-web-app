using CSM.Web.Models.OfflineSync;

namespace CSM.Web.Services;

public sealed class OfflineSyncCoordinator : IOfflineSyncCoordinator
{
    private readonly IOfflineSyncService _offlineSyncService;
    private readonly IOfflineSyncApi _offlineSyncApi;

    public OfflineSyncCoordinator(
        IOfflineSyncService offlineSyncService,
        IOfflineSyncApi offlineSyncApi)
    {
        _offlineSyncService = offlineSyncService;
        _offlineSyncApi = offlineSyncApi;
    }

    public async Task SyncAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await _offlineSyncService.IsOnlineAsync())
        {
            return;
        }

        var operations =
            await _offlineSyncService.GetPendingAsync();

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessOperationAsync(
                operation,
                cancellationToken);
        }
    }

    private async Task ProcessOperationAsync(
        OfflineSyncOperation operation,
        CancellationToken cancellationToken)
    {
        await _offlineSyncApi.ExecuteAsync(
            operation,
            cancellationToken);

        await _offlineSyncService.RemoveAsync(
            operation.Id);
    }
}