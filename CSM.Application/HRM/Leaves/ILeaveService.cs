using CSM.Application.HRM.Leaves.Dtos;

namespace CSM.Application.HRM.Leaves;

public interface ILeaveService
{
    Task<IReadOnlyCollection<LeaveEmployeeOptionResponse>> GetEmployeeOptionsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LeaveResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);

    Task<LeaveResponse> GetByIdAsync(
        Guid currentUserId,
        Guid leaveRequestId,
        CancellationToken cancellationToken = default);

    Task<LeaveResponse> CreateAsync(
        Guid currentUserId,
        CreateLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task<LeaveResponse> ReviewAsync(
        Guid currentUserId,
        Guid leaveRequestId,
        ReviewLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task<LeaveResponse> CancelAsync(
        Guid currentUserId,
        Guid leaveRequestId,
        CancelLeaveRequest request,
        CancellationToken cancellationToken = default);
}
