using CSM.Application.HRM.Timesheets.Dtos;

namespace CSM.Application.HRM.Timesheets;

public interface ITimesheetService
{
    Task<IReadOnlyCollection<TimesheetResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);

    Task<TimesheetResponse> GetByIdAsync(
        Guid currentUserId,
        Guid timesheetId,
        CancellationToken cancellationToken = default);

    Task<TimesheetResponse> CreateAsync(
        Guid currentUserId,
        CreateTimesheetRequest request,
        CancellationToken cancellationToken = default);

    Task<TimesheetResponse> SubmitAsync(
        Guid currentUserId,
        Guid timesheetId,
        SubmitTimesheetRequest request,
        CancellationToken cancellationToken = default);

    Task<TimesheetResponse> ReviewAsync(
        Guid currentUserId,
        Guid timesheetId,
        ReviewTimesheetRequest request,
        CancellationToken cancellationToken = default);

    Task<TimesheetResponse> CancelAsync(
        Guid currentUserId,
        Guid timesheetId,
        CancelTimesheetRequest request,
        CancellationToken cancellationToken = default);

    Task<TimesheetResponse> GenerateAsync(
        Guid currentUserId,
        GenerateTimesheetRequest request,
        CancellationToken cancellationToken = default);
}
