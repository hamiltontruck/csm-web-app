using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface ILeaveService
{
    Task<IReadOnlyCollection<LeaveEmployeeOptionResponse>> GetEmployeeOptionsAsync();

    Task<IReadOnlyCollection<LeaveResponse>> GetAllAsync(
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null);

    Task<LeaveResponse?> GetByIdAsync(Guid leaveRequestId);

    Task<LeaveResponse> CreateAsync(CreateLeaveRequest request);

    Task<LeaveResponse> ReviewAsync(
        Guid leaveRequestId,
        ReviewLeaveRequest request);

    Task<LeaveResponse> CancelAsync(
        Guid leaveRequestId,
        CancelLeaveRequest request);
}