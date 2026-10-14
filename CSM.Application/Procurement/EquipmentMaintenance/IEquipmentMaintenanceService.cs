using CSM.Application.Procurement.EquipmentMaintenance.Dtos;

namespace CSM.Application.Procurement.EquipmentMaintenance;

public interface IEquipmentMaintenanceService
{
    Task<IReadOnlyCollection<EquipmentMaintenanceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? equipmentId = null,
        bool? completedOnly = null,
        CancellationToken cancellationToken = default);

    Task<EquipmentMaintenanceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid maintenanceId,
        CancellationToken cancellationToken = default);

    Task<EquipmentMaintenanceResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentMaintenanceRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentMaintenanceResponse> UpdateAsync(
        Guid currentUserId,
        Guid maintenanceId,
        UpdateEquipmentMaintenanceRequest request,
        CancellationToken cancellationToken = default);
}
