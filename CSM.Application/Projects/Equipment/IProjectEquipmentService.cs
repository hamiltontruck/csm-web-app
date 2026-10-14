using CSM.Application.Projects.Equipment.Dtos;

namespace CSM.Application.Projects.Equipment;

public interface IProjectEquipmentService
{
    Task<IReadOnlyCollection<EquipmentResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<EquipmentResponse> CreateAsync(
        CreateEquipmentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}