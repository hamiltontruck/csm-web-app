using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface IAttendanceService
{
    Task<IReadOnlyList<AttendanceResponse>> GetAttendancesAsync(
        Guid? employeeId = null,
        Guid? constructionSiteId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null);

    Task<AttendanceResponse> GetAttendanceAsync(
        Guid attendanceId);

    Task<AttendanceResponse> CheckInAsync(
        CheckInRequest request);

    Task<AttendanceResponse> CheckOutAsync(
        Guid attendanceId,
        CheckOutRequest request);

    Task<AttendanceResponse> ApproveAsync(
        Guid attendanceId,
        ApproveAttendanceRequest request);

    Task<AttendanceResponse> OverrideAsync(
        Guid attendanceId,
        OverrideAttendanceRequest request);
}