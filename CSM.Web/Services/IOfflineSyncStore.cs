using CSM.Web.Models.OfflineSync;

namespace CSM.Web.Services;

public interface IOfflineSyncStore
{
    Task AddAsync(
        OfflineSyncOperation operation);

    Task<IReadOnlyList<OfflineSyncOperation>> GetAllAsync();

    Task RemoveAsync(Guid id);
}