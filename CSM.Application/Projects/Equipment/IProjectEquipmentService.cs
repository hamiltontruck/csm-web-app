using CSM.Application.Projects.Equipment.Dtos;

namespace CSM.Application.Projects.Equipment;

public interface IProjectEquipmentService
{
    Task<IReadOnlyCollection<EquipmentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}