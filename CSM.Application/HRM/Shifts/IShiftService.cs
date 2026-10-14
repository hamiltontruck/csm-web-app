using CSM.Application.HRM.Shifts.Dtos;

namespace CSM.Application.HRM.Shifts;

public interface IShiftService
{
    Task<IReadOnlyCollection<ShiftResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ShiftResponse> GetByIdAsync(
        Guid currentUserId,
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<ShiftResponse> CreateAsync(
        Guid currentUserId,
        CreateShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<ShiftResponse> UpdateAsync(
        Guid currentUserId,
        Guid shiftId,
        UpdateShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<ShiftResponse> ChangeActiveStatusAsync(
        Guid currentUserId,
        Guid shiftId,
        ChangeShiftActiveStatusRequest request,
        CancellationToken cancellationToken = default);
}