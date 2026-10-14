using CSM.Web.Models.OfflineSync;
using Microsoft.JSInterop;

namespace CSM.Web.Services;

public sealed class OfflineSyncStore : IOfflineSyncStore
{
    private readonly IJSRuntime _jsRuntime;

    public OfflineSyncStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task AddAsync(
        OfflineSyncOperation operation)
    {
        await _jsRuntime.InvokeVoidAsync(
            "csmOfflineSync.add",
            operation);
    }

    public async Task<IReadOnlyList<OfflineSyncOperation>> GetAllAsync()
    {
        var operations =
            await _jsRuntime.InvokeAsync<
                List<OfflineSyncOperation>>(
                "csmOfflineSync.getAll");

        return operations;
    }

    public async Task RemoveAsync(Guid id)
    {
        await _jsRuntime.InvokeVoidAsync(
            "csmOfflineSync.remove",
            id.ToString());
    }
}