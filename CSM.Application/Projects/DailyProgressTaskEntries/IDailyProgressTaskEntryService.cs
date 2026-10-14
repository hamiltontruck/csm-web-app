using CSM.Application.Projects.DailyProgressTaskEntries.Dtos;

namespace CSM.Application.Projects.DailyProgressTaskEntries;

public interface IDailyProgressTaskEntryService
{
    Task<DailyProgressTaskEntryResponse> CreateAsync(
        Guid currentUserId,
        CreateDailyProgressTaskEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentEntryResponse> AddEquipmentAsync(
        Guid currentUserId,
        CreateEquipmentEntryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DailyProgressTaskEntryResponse>> GetByLogAsync(
        Guid currentUserId,
        Guid dailyProgressLogId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EquipmentEntryResponse>> GetEquipmentAsync(
        Guid currentUserId,
        Guid taskEntryId,
        CancellationToken cancellationToken = default);
}