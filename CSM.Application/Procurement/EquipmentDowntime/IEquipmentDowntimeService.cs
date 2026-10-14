using CSM.Application.Procurement.EquipmentDowntime.Dtos;

namespace CSM.Application.Procurement.EquipmentDowntime;

public interface IEquipmentDowntimeService
{
    Task<IReadOnlyCollection<EquipmentDowntimeResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? equipmentId = null,
        Guid? constructionSiteId = null,
        bool? openOnly = null,
        CancellationToken cancellationToken = default);

    Task<EquipmentDowntimeResponse> GetByIdAsync(
        Guid currentUserId,
        Guid downtimeId,
        CancellationToken cancellationToken = default);

    Task<EquipmentDowntimeResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentDowntimeResponse> UpdateAsync(
        Guid currentUserId,
        Guid downtimeId,
        UpdateEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentDowntimeResponse> CloseAsync(
        Guid currentUserId,
        Guid downtimeId,
        DateTime endedAtUtc,
        string? resolution,
        CancellationToken cancellationToken = default);
}
