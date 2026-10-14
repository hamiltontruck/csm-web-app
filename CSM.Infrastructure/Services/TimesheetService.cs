using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Timesheets;
using CSM.Application.HRM.Timesheets.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class TimesheetService : ITimesheetService
{
    private readonly ApplicationDbContext _dbContext;

    public TimesheetService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<TimesheetResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var query = _dbContext.Timesheets
            .AsNoTracking()
            .Include(x => x.Employee)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        if (employeeId.HasValue)
        {
            query = query.Where(
                x => x.EmployeeId == employeeId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(
                x => x.PeriodEndDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(
                x => x.PeriodStartDate <= toDate.Value);
        }

        return await query
            .OrderByDescending(x => x.PeriodStartDate)
            .ThenBy(x => x.Employee.EmployeeNumber)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<TimesheetResponse> GetByIdAsync(
        Guid currentUserId,
        Guid timesheetId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var timesheet = await GetTimesheetAsync(
            timesheetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            timesheet.CompanyId);

        return Map(timesheet);
    }

    public async Task<TimesheetResponse> CreateAsync(
        Guid currentUserId,
        CreateTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidatePeriod(
            request.PeriodStartDate,
            request.PeriodEndDate);

        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.Id == request.EmployeeId,
                cancellationToken)
            ?? throw new TimesheetManagementException(
                "Employee was not found.");

        EnsureCompanyAccess(
            actor,
            employee.CompanyId);

        var duplicateExists =
            await _dbContext.Timesheets.AnyAsync(
                x =>
                    x.EmployeeId == employee.Id &&
                    x.CompanyId == employee.CompanyId &&
                    x.PeriodStartDate ==
                        request.PeriodStartDate &&
                    x.PeriodEndDate ==
                        request.PeriodEndDate,
                cancellationToken);

        if (duplicateExists)
        {
            throw new TimesheetManagementException(
                "A timesheet already exists for this employee and period.");
        }

        var overlappingExists =
            await _dbContext.Timesheets.AnyAsync(
                x =>
                    x.EmployeeId == employee.Id &&
                    x.CompanyId == employee.CompanyId &&
                    x.Status != TimesheetStatus.Cancelled &&
                    x.PeriodStartDate <=
                        request.PeriodEndDate &&
                    x.PeriodEndDate >=
                        request.PeriodStartDate,
                cancellationToken);

        if (overlappingExists)
        {
            throw new TimesheetManagementException(
                "An overlapping active timesheet already exists for this employee.");
        }

        var attendance = await _dbContext.Attendances
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == employee.CompanyId &&
                    x.EmployeeId == employee.Id &&
                    x.AttendanceDate >=
                        request.PeriodStartDate &&
                    x.AttendanceDate <=
                        request.PeriodEndDate &&
                    x.CheckOutAtUtc.HasValue &&
                    (!x.RequiresApproval ||
                     x.IsApproved))
            .ToListAsync(cancellationToken);

        var regularHours =
            attendance.Sum(x => x.RegularHours);

        var overtimeHours =
            attendance.Sum(x => x.OvertimeHours);

        var timesheet = new Timesheet
        {
            Id = Guid.NewGuid(),
            CompanyId = employee.CompanyId,
            EmployeeId = employee.Id,
            PeriodStartDate =
                request.PeriodStartDate,
            PeriodEndDate =
                request.PeriodEndDate,
            RegularHours = regularHours,
            OvertimeHours = overtimeHours,
            TotalHours =
                regularHours + overtimeHours,
            Status = TimesheetStatus.Draft,
            Remarks = Clean(request.Remarks)
        };

        _dbContext.Timesheets.Add(timesheet);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        timesheet.Employee = employee;

        return Map(timesheet);
    }

    public async Task<TimesheetResponse> SubmitAsync(
        Guid currentUserId,
        Guid timesheetId,
        SubmitTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var timesheet = await GetTimesheetAsync(
            timesheetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            timesheet.CompanyId);

        if (timesheet.Status != TimesheetStatus.Draft &&
            timesheet.Status != TimesheetStatus.Rejected)
        {
            throw new TimesheetManagementException(
                "Only Draft or Rejected timesheets can be submitted.");
        }

        await RecalculateHoursAsync(
            timesheet,
            cancellationToken);

        timesheet.Status =
            TimesheetStatus.Submitted;

        timesheet.SubmittedBy =
            actor.Id;

        timesheet.SubmittedAtUtc =
            DateTime.UtcNow;

        timesheet.ApprovedBy = null;
        timesheet.ApprovedAtUtc = null;
        timesheet.ApprovalRemarks = null;

        if (!string.IsNullOrWhiteSpace(
                request.Remarks))
        {
            timesheet.Remarks =
                request.Remarks.Trim();
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(timesheet);
    }

    public async Task<TimesheetResponse> ReviewAsync(
        Guid currentUserId,
        Guid timesheetId,
        ReviewTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        if (request.Status !=
                TimesheetStatus.Approved &&
            request.Status !=
                TimesheetStatus.Rejected)
        {
            throw new TimesheetManagementException(
                "Review status must be Approved or Rejected.");
        }

        var timesheet = await GetTimesheetAsync(
            timesheetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            timesheet.CompanyId);

        if (timesheet.Status !=
            TimesheetStatus.Submitted)
        {
            throw new TimesheetManagementException(
                "Only Submitted timesheets can be reviewed.");
        }

        await RecalculateHoursAsync(
            timesheet,
            cancellationToken);

        timesheet.Status =
            request.Status;

        timesheet.ApprovedBy =
            actor.Id;

        timesheet.ApprovedAtUtc =
            DateTime.UtcNow;

        timesheet.ApprovalRemarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(timesheet);
    }

    public async Task<TimesheetResponse> CancelAsync(
        Guid currentUserId,
        Guid timesheetId,
        CancelTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var timesheet = await GetTimesheetAsync(
            timesheetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            timesheet.CompanyId);

        if (timesheet.Status ==
            TimesheetStatus.Cancelled)
        {
            throw new TimesheetManagementException(
                "Timesheet is already cancelled.");
        }

        timesheet.Status =
            TimesheetStatus.Cancelled;

        if (!string.IsNullOrWhiteSpace(
                request.Remarks))
        {
            timesheet.Remarks =
                request.Remarks.Trim();
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(timesheet);
    }

    public async Task<TimesheetResponse> GenerateAsync(
        Guid currentUserId,
        GenerateTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        EnsureCanManage(actor);

        var start = new DateOnly(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var createRequest = new CreateTimesheetRequest(
            request.EmployeeId,
            start,
            end,
            "Generated from approved attendance");

        return await CreateAsync(
            currentUserId,
            createRequest,
            cancellationToken);
    }

    private async Task RecalculateHoursAsync(
        Timesheet timesheet,
        CancellationToken cancellationToken)
    {
        var attendance =
            await _dbContext.Attendances
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId ==
                            timesheet.CompanyId &&
                        x.EmployeeId ==
                            timesheet.EmployeeId &&
                        x.AttendanceDate >=
                            timesheet.PeriodStartDate &&
                        x.AttendanceDate <=
                            timesheet.PeriodEndDate &&
                        x.CheckOutAtUtc.HasValue &&
                        (!x.RequiresApproval ||
                         x.IsApproved))
                .ToListAsync(cancellationToken);

        timesheet.RegularHours =
            attendance.Sum(x => x.RegularHours);

        timesheet.OvertimeHours =
            attendance.Sum(x => x.OvertimeHours);

        timesheet.TotalHours =
            timesheet.RegularHours +
            timesheet.OvertimeHours;
    }

    private async Task<Timesheet> GetTimesheetAsync(
        Guid timesheetId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Timesheets
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(
                x => x.Id == timesheetId,
                cancellationToken)
            ?? throw new TimesheetManagementException(
                "Timesheet was not found.");
    }

    private async Task<User> GetActorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var actor = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken)
            ?? throw new TimesheetManagementException(
                "Current user was not found.");

        if (!actor.IsActive)
        {
            throw new TimesheetManagementException(
                "Current user account is inactive.");
        }

        return actor;
    }

    private static void ValidatePeriod(
        DateOnly startDate,
        DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new TimesheetManagementException(
                "Period end date cannot be before period start date.");
        }

        var numberOfDays =
            endDate.DayNumber -
            startDate.DayNumber + 1;

        if (numberOfDays > 31)
        {
            throw new TimesheetManagementException(
                "Timesheet period cannot exceed 31 days.");
        }
    }

    private static void EnsureCanRead(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new TimesheetManagementException(
            "You are not authorized to access timesheets.");
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new TimesheetManagementException(
            "You are not authorized to manage timesheets.");
    }

    private static void EnsureCanApprove(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new TimesheetManagementException(
            "You are not authorized to approve timesheets.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new TimesheetManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static void EnsureCompanyAccess(
        User actor,
        Guid companyId)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != companyId)
        {
            throw new TimesheetManagementException(
                "You cannot access timesheets outside your company.");
        }
    }

    private static bool IsSuperAdmin(
        User actor)
    {
        return HasRole(
            actor,
            AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User actor,
        string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static TimesheetResponse Map(
        Timesheet timesheet)
    {
        var employeeName = string.Join(
            " ",
            new[]
            {
                timesheet.Employee.FirstName,
                timesheet.Employee.MiddleName,
                timesheet.Employee.LastName
            }
            .Where(x =>
                !string.IsNullOrWhiteSpace(x)));

        return new TimesheetResponse(
            timesheet.Id,
            timesheet.CompanyId,
            timesheet.EmployeeId,
            timesheet.Employee.EmployeeNumber,
            employeeName,
            timesheet.PeriodStartDate,
            timesheet.PeriodEndDate,
            timesheet.RegularHours,
            timesheet.OvertimeHours,
            timesheet.TotalHours,
            timesheet.Status,
            timesheet.SubmittedBy,
            timesheet.SubmittedAtUtc,
            timesheet.ApprovedBy,
            timesheet.ApprovedAtUtc,
            timesheet.ApprovalRemarks,
            timesheet.Remarks,
            timesheet.CreatedAtUtc);
    }
}
