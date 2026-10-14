using CSM.Application.Inventory.DailyMaterialUsage.Dtos;

namespace CSM.Application.Inventory.DailyMaterialUsage;

public interface IDailyMaterialUsageService
{
    Task<DailyMaterialUsageResponse> CreateAsync(
        Guid currentUserId,
        CreateDailyMaterialUsageRequest request,
        CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<DailyMaterialUsageResponse>> GetAllAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default);
}
