using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface ITimesheetService
{
    Task<IReadOnlyCollection<TimesheetResponse>> GetAllAsync(
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null);

    Task<TimesheetResponse?> GetByIdAsync(
        Guid timesheetId);

    Task<TimesheetResponse> CreateAsync(
        CreateTimesheetRequest request);

    Task<TimesheetResponse> SubmitAsync(
        Guid timesheetId,
        SubmitTimesheetRequest request);

    Task<TimesheetResponse> ReviewAsync(
        Guid timesheetId,
        ReviewTimesheetRequest request);

    Task<TimesheetResponse> CancelAsync(
        Guid timesheetId,
        CancelTimesheetRequest request);
}