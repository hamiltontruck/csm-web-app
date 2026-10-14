using CSM.Application.Procurement.Equipment.Dtos;
using CSM.Domain.Enums;

namespace CSM.Application.Procurement.Equipment;

public interface IEquipmentService
{
    Task<IReadOnlyCollection<EquipmentResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive = null,
        EquipmentStatus? status = null,
        EquipmentOwnershipType? ownershipType = null,
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid equipmentId,
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse> UpdateAsync(
        Guid currentUserId,
        Guid equipmentId,
        UpdateEquipmentRequest request,
        CancellationToken cancellationToken = default);
}
