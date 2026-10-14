using CSM.Web.Models.OfflineSync;
using Microsoft.JSInterop;

namespace CSM.Web.Services;

public sealed class OfflineSyncService : IOfflineSyncService
{
    private readonly IOfflineSyncStore _store;

    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<OfflineSyncService>? _dotNetReference;

    public OfflineSyncService(
        IOfflineSyncStore store,
        IJSRuntime jsRuntime)
    {
        _store = store;
        _jsRuntime = jsRuntime;
    }

    public event Action? ConnectionRestored;

    public event Action? ConnectionLost;

    public async Task QueueAsync(
        OfflineSyncOperation operation)
    {
        operation.Status = OfflineSyncStatus.Pending;

        await _store.AddAsync(operation);
    }

    public async Task<IReadOnlyList<OfflineSyncOperation>> GetPendingAsync()
    {
        var operations = await _store.GetAllAsync();

        return operations
            .Where(x => x.Status == OfflineSyncStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();
    }

    public async Task RemoveAsync(Guid operationId)
    {
        await _store.RemoveAsync(operationId);
    }

    public async Task<bool> IsOnlineAsync()
    {
        return await _jsRuntime.InvokeAsync<bool>(
            "csmConnectionState.isOnline");
    }

    public async Task InitializeConnectionMonitoringAsync()
    {
        _dotNetReference ??=
            DotNetObjectReference.Create(this);

        await _jsRuntime.InvokeVoidAsync(
            "csmConnectionState.registerConnectionHandlers",
            _dotNetReference);
    }

    [JSInvokable]
    public void OnOnline()
    {
        ConnectionRestored?.Invoke();
    }

    [JSInvokable]
    public void OnOffline()
    {
        ConnectionLost?.Invoke();
    }


}