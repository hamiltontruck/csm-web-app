using CSM.Application.HRM.Attendances.Dtos;

namespace CSM.Application.HRM.Attendances;

public interface IAttendanceService
{
    Task<IReadOnlyCollection<AttendanceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        Guid? constructionSiteId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);

    Task<AttendanceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid attendanceId,
        CancellationToken cancellationToken = default);

    Task<AttendanceResponse> CheckInAsync(
        Guid currentUserId,
        CheckInRequest request,
        CancellationToken cancellationToken = default);

    Task<AttendanceResponse> CheckOutAsync(
        Guid currentUserId,
        Guid attendanceId,
        CheckOutRequest request,
        CancellationToken cancellationToken = default);

    Task<AttendanceResponse> ApproveAsync(
        Guid currentUserId,
        Guid attendanceId,
        ApproveAttendanceRequest request,
        CancellationToken cancellationToken = default);

    Task<AttendanceResponse> OverrideAsync(
        Guid currentUserId,
        Guid attendanceId,
        OverrideAttendanceRequest request,
        CancellationToken cancellationToken = default);


}
