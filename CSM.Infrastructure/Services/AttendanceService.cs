using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Attendances;
using CSM.Application.HRM.Attendances.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _dbContext;

    public AttendanceService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<AttendanceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        Guid? constructionSiteId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        if (fromDate.HasValue &&
            toDate.HasValue &&
            toDate.Value < fromDate.Value)
        {
            throw new AttendanceManagementException(
                "To date cannot be earlier than from date.");
        }

        IQueryable<Attendance> query =
            _dbContext.Attendances
                .AsNoTracking()
                .Include(x => x.Employee)
                .Include(x => x.ConstructionSite)
                .Include(x => x.Shift);

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

        if (constructionSiteId.HasValue)
        {
            query = query.Where(
                x => x.ConstructionSiteId ==
                     constructionSiteId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(
                x => x.AttendanceDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(
                x => x.AttendanceDate <= toDate.Value);
        }

        var attendances = await query
            .OrderByDescending(x => x.AttendanceDate)
            .ThenBy(x => x.Employee.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return attendances
            .Select(Map)
            .ToArray();
    }

    public async Task<AttendanceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid attendanceId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var attendance = await GetAttendanceAsync(
            attendanceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            attendance.CompanyId);

        return Map(attendance);
    }

    public async Task<AttendanceResponse> CheckInAsync(
        Guid currentUserId,
        CheckInRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        ValidateCoordinates(
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters);

        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.Id == request.EmployeeId,
                cancellationToken)
            ?? throw new AttendanceManagementException(
                "Employee was not found.");

        if (employee.CompanyId != companyId)
        {
            throw new AttendanceManagementException(
                "Employee does not belong to your company.");
        }

        EnsureEmployeeCanAttend(employee);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new AttendanceManagementException(
                "Construction site was not found.");

        if (site.CompanyId != companyId)
        {
            throw new AttendanceManagementException(
                "Construction site does not belong to your company.");
        }

        if (!site.IsActive)
        {
            throw new AttendanceManagementException(
                "Attendance cannot be recorded for an inactive construction site.");
        }

        var attendanceDate =
            DateOnly.FromDateTime(request.CheckInAtUtc);

        var hasAssignment =
            await _dbContext.SiteAssignments.AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.EmployeeId == employee.Id &&
                    x.ConstructionSiteId == site.Id &&
                    x.StartDate <= attendanceDate &&
                    (!x.EndDate.HasValue ||
                     x.EndDate.Value >= attendanceDate),
                cancellationToken);

        if (!hasAssignment)
        {
            throw new AttendanceManagementException(
                "Employee does not have an active assignment to this construction site on the attendance date.");
        }

        var duplicateExists =
            await _dbContext.Attendances.AnyAsync(
                x =>
                    x.EmployeeId == employee.Id &&
                    x.ConstructionSiteId == site.Id &&
                    x.AttendanceDate == attendanceDate,
                cancellationToken);

        if (duplicateExists)
        {
            throw new AttendanceManagementException(
                "Attendance has already been recorded for this employee, site, and date.");
        }

        Shift? shift = null;

        if (request.ShiftId.HasValue)
        {
            shift = await _dbContext.Shifts
                .SingleOrDefaultAsync(
                    x => x.Id == request.ShiftId.Value,
                    cancellationToken)
                ?? throw new AttendanceManagementException(
                    "Shift was not found.");

            if (shift.CompanyId != companyId)
            {
                throw new AttendanceManagementException(
                    "Shift does not belong to your company.");
            }

            if (!shift.IsActive)
            {
                throw new AttendanceManagementException(
                    "Attendance cannot use an inactive shift.");
            }
        }

        var status = DetermineStatus(
            request.CheckInAtUtc,
            shift);

        var withinGeofence = IsWithinGeofence(
            site,
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters);

        var requiresApproval =
            request.Source == AttendanceSource.Manual ||
            (HasConfiguredGeofence(site) &&
             !withinGeofence);

        var attendance = new Attendance
        {
            CompanyId = companyId,
            EmployeeId = employee.Id,
            ConstructionSiteId = site.Id,
            ShiftId = shift?.Id,
            AttendanceDate = attendanceDate,
            CheckInAtUtc = request.CheckInAtUtc,
            CheckInLatitude = request.Latitude,
            CheckInLongitude = request.Longitude,
            CheckInAccuracyMeters =
                request.AccuracyMeters,
            IsCheckInWithinGeofence =
                withinGeofence,
            Status = status,
            Source = request.Source,
            RegularHours = 0m,
            OvertimeHours = 0m,
            BiometricReference =
                Clean(request.BiometricReference),
            RequiresApproval = requiresApproval,
            IsApproved = false,
            Remarks = Clean(request.Remarks),
            Employee = employee,
            ConstructionSite = site,
            Shift = shift
        };

        _dbContext.Attendances.Add(attendance);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(attendance);
    }

    public async Task<AttendanceResponse> CheckOutAsync(
        Guid currentUserId,
        Guid attendanceId,
        CheckOutRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        ValidateCoordinates(
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters);

        var attendance = await GetAttendanceAsync(
            attendanceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            attendance.CompanyId);

        if (!attendance.CheckInAtUtc.HasValue)
        {
            throw new AttendanceManagementException(
                "Attendance does not have a check-in time.");
        }

        if (attendance.CheckOutAtUtc.HasValue)
        {
            throw new AttendanceManagementException(
                "Attendance has already been checked out.");
        }

        if (request.CheckOutAtUtc <=
            attendance.CheckInAtUtc.Value)
        {
            throw new AttendanceManagementException(
                "Check-out time must be later than check-in time.");
        }

        var workedHours =
            (decimal)(
                request.CheckOutAtUtc -
                attendance.CheckInAtUtc.Value)
            .TotalHours;

        if (workedHours > 24m)
        {
            throw new AttendanceManagementException(
                "Attendance duration cannot exceed 24 hours.");
        }

        decimal standardHours;

        if (attendance.Shift is not null)
        {
            standardHours =
                attendance.Shift.StandardHours;
        }
        else
        {
            standardHours = 8m;
        }

        attendance.RegularHours =
            Math.Round(
                Math.Min(
                    workedHours,
                    standardHours),
                2);

        attendance.OvertimeHours =
            Math.Round(
                Math.Max(
                    workedHours - standardHours,
                    0m),
                2);

        attendance.CheckOutAtUtc =
            request.CheckOutAtUtc;

        attendance.CheckOutLatitude =
            request.Latitude;

        attendance.CheckOutLongitude =
            request.Longitude;

        attendance.CheckOutAccuracyMeters =
            request.AccuracyMeters;

        attendance.IsCheckOutWithinGeofence =
            IsWithinGeofence(
                attendance.ConstructionSite,
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters);

        if (HasConfiguredGeofence(
                attendance.ConstructionSite) &&
            !attendance.IsCheckOutWithinGeofence)
        {
            attendance.RequiresApproval = true;
        }

        var remarks = Clean(request.Remarks);

        if (remarks is not null)
        {
            attendance.Remarks = remarks;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(attendance);
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
                cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            throw new AttendanceManagementException(
                "Current user was not found or is inactive.");
        }

        return actor;
    }

    private async Task<Attendance> GetAttendanceAsync(
        Guid attendanceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Attendances
            .Include(x => x.Employee)
            .Include(x => x.ConstructionSite)
            .Include(x => x.Shift)
            .SingleOrDefaultAsync(
                x => x.Id == attendanceId,
                cancellationToken)
            ?? throw new AttendanceManagementException(
                "Attendance was not found.");
    }

    public async Task<AttendanceResponse> ApproveAsync(
        Guid currentUserId,
        Guid attendanceId,
        ApproveAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        var attendance = await GetAttendanceAsync(
            attendanceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            attendance.CompanyId);

        if (!attendance.RequiresApproval)
        {
            throw new AttendanceManagementException(
                "This attendance does not require approval.");
        }

        if (attendance.IsApproved)
        {
            throw new AttendanceManagementException(
                "Attendance has already been approved.");
        }

        if (!attendance.CheckInAtUtc.HasValue)
        {
            throw new AttendanceManagementException(
                "Attendance without a check-in cannot be approved.");
        }

        attendance.IsApproved = true;
        attendance.ApprovedBy = actor.Id;
        attendance.ApprovedAtUtc = DateTime.UtcNow;

        var remarks = Clean(request.Remarks);

        if (remarks is not null)
        {
            attendance.Remarks = remarks;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(attendance);
    }

    public async Task<AttendanceResponse> OverrideAsync(
        Guid currentUserId,
        Guid attendanceId,
        OverrideAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanOverride(actor);

        var attendance = await GetAttendanceAsync(
            attendanceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            attendance.CompanyId);

        var reason = Clean(request.Reason);

        if (reason is null)
        {
            throw new AttendanceManagementException(
                "A reason is required when manually overriding attendance.");
        }

        if (!request.CheckInAtUtc.HasValue)
        {
            throw new AttendanceManagementException(
                "Check-in time is required.");
        }

        if (request.CheckOutAtUtc.HasValue &&
            request.CheckOutAtUtc.Value <= request.CheckInAtUtc.Value)
        {
            throw new AttendanceManagementException(
                "Check-out time must be later than check-in time.");
        }

        if (request.CheckOutAtUtc.HasValue)
        {
            var workedHours =
                (decimal)(
                    request.CheckOutAtUtc.Value -
                    request.CheckInAtUtc.Value)
                .TotalHours;

            if (workedHours > 24m)
            {
                throw new AttendanceManagementException(
                    "Attendance duration cannot exceed 24 hours.");
            }

            var standardHours =
                attendance.Shift?.StandardHours ?? 8m;

            attendance.RegularHours =
                Math.Round(
                    Math.Min(
                        workedHours,
                        standardHours),
                    2);

            attendance.OvertimeHours =
                Math.Round(
                    Math.Max(
                        workedHours - standardHours,
                        0m),
                    2);
        }
        else
        {
            attendance.RegularHours = 0m;
            attendance.OvertimeHours = 0m;
        }

        attendance.CheckInAtUtc =
            request.CheckInAtUtc;

        attendance.CheckOutAtUtc =
            request.CheckOutAtUtc;

        attendance.AttendanceDate =
            DateOnly.FromDateTime(
                request.CheckInAtUtc.Value);

        attendance.Status =
            request.Status;

        attendance.ManualOverrideReason =
            reason;

        attendance.RequiresApproval = true;

        // Any previous approval becomes invalid after
        // the attendance record has been changed.
        attendance.IsApproved = false;
        attendance.ApprovedBy = null;
        attendance.ApprovedAtUtc = null;

        var remarks = Clean(request.Remarks);

        if (remarks is not null)
        {
            attendance.Remarks = remarks;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(attendance);
    }

    private static AttendanceStatus DetermineStatus(
        DateTime checkInAtUtc,
        Shift? shift)
    {
        if (shift is null)
        {
            return AttendanceStatus.Present;
        }

        var checkInTime =
            TimeOnly.FromDateTime(checkInAtUtc);

        var lateThreshold =
            shift.StartTime.AddMinutes(
                shift.GracePeriodMinutes);

        return checkInTime > lateThreshold
            ? AttendanceStatus.Late
            : AttendanceStatus.Present;
    }

    private static void EnsureEmployeeCanAttend(
        Employee employee)
    {
        if (employee.Status ==
            EmployeeStatus.Terminated)
        {
            throw new AttendanceManagementException(
                "Attendance cannot be recorded for a terminated employee.");
        }

        if (employee.Status ==
            EmployeeStatus.Inactive)
        {
            throw new AttendanceManagementException(
                "Attendance cannot be recorded for an inactive employee.");
        }

        if (employee.Status ==
            EmployeeStatus.Suspended)
        {
            throw new AttendanceManagementException(
                "Attendance cannot be recorded for a suspended employee.");
        }

        if (employee.Status ==
            EmployeeStatus.OnLeave)
        {
            throw new AttendanceManagementException(
                "Employee is currently marked as on leave.");
        }
    }

    private static void ValidateCoordinates(
        decimal? latitude,
        decimal? longitude,
        decimal? accuracyMeters)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new AttendanceManagementException(
                "Latitude and longitude must be supplied together.");
        }

        if (latitude.HasValue &&
            (latitude.Value < -90m ||
             latitude.Value > 90m))
        {
            throw new AttendanceManagementException(
                "Latitude must be between -90 and 90.");
        }

        if (longitude.HasValue &&
            (longitude.Value < -180m ||
             longitude.Value > 180m))
        {
            throw new AttendanceManagementException(
                "Longitude must be between -180 and 180.");
        }

        if (accuracyMeters.HasValue &&
            accuracyMeters.Value < 0m)
        {
            throw new AttendanceManagementException(
                "GPS accuracy cannot be negative.");
        }
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

        throw new AttendanceManagementException(
            "You are not authorized to approve attendance.");
    }
    private static void EnsureCanOverride(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new AttendanceManagementException(
            "You are not authorized to override attendance.");
    }

    private static bool HasConfiguredGeofence(
        ConstructionSite site)
    {
        return site.Latitude.HasValue &&
               site.Longitude.HasValue &&
               site.GeofenceRadiusMeters.HasValue &&
               site.GeofenceRadiusMeters.Value > 0m;
    }

    private static bool IsWithinGeofence(
        ConstructionSite site,
        decimal? latitude,
        decimal? longitude,
        decimal? accuracyMeters)
    {
        if (!HasConfiguredGeofence(site) ||
            !latitude.HasValue ||
            !longitude.HasValue)
        {
            return false;
        }

        var distanceMeters = CalculateDistanceMeters(
            site.Latitude!.Value,
            site.Longitude!.Value,
            latitude.Value,
            longitude.Value);

        var accuracyAllowance =
            accuracyMeters.GetValueOrDefault();

        return distanceMeters <=
               site.GeofenceRadiusMeters!.Value +
               accuracyAllowance;
    }

    private static decimal CalculateDistanceMeters(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2)
    {
        const double earthRadiusMeters = 6371000d;

        var lat1 =
            DegreesToRadians((double)latitude1);

        var lat2 =
            DegreesToRadians((double)latitude2);

        var deltaLatitude =
            DegreesToRadians(
                (double)(latitude2 - latitude1));

        var deltaLongitude =
            DegreesToRadians(
                (double)(longitude2 - longitude1));

        var a =
            Math.Sin(deltaLatitude / 2d) *
            Math.Sin(deltaLatitude / 2d) +
            Math.Cos(lat1) *
            Math.Cos(lat2) *
            Math.Sin(deltaLongitude / 2d) *
            Math.Sin(deltaLongitude / 2d);

        var c =
            2d * Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1d - a));

        return (decimal)(
            earthRadiusMeters * c);
    }

    private static double DegreesToRadians(
        double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void EnsureCanRead(User actor)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new AttendanceManagementException(
            "You are not authorized to access attendance.");
    }

    private static void EnsureCanModify(User actor)
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

        throw new AttendanceManagementException(
            "You are not authorized to record attendance.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new AttendanceManagementException(
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
            throw new AttendanceManagementException(
                "You cannot access attendance outside your company.");
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

    private static AttendanceResponse Map(
        Attendance attendance)
    {
        var employeeName = string.Join(
            " ",
            new[]
            {
                attendance.Employee.FirstName,
                attendance.Employee.MiddleName,
                attendance.Employee.LastName
            }
            .Where(x =>
                !string.IsNullOrWhiteSpace(x)));

        return new AttendanceResponse(
            attendance.Id,
            attendance.CompanyId,
            attendance.EmployeeId,
            attendance.Employee.EmployeeNumber,
            employeeName,
            attendance.ConstructionSiteId,
            attendance.ConstructionSite.Name,
            attendance.ShiftId,
            attendance.Shift?.Code,
            attendance.Shift?.Name,
            attendance.AttendanceDate,
            attendance.CheckInAtUtc,
            attendance.CheckOutAtUtc,
            attendance.CheckInLatitude,
            attendance.CheckInLongitude,
            attendance.CheckOutLatitude,
            attendance.CheckOutLongitude,
            attendance.CheckInAccuracyMeters,
            attendance.CheckOutAccuracyMeters,
            attendance.IsCheckInWithinGeofence,
            attendance.IsCheckOutWithinGeofence,
            attendance.Status,
            attendance.Source,
            attendance.RegularHours,
            attendance.OvertimeHours,
            attendance.BiometricReference,
            attendance.RequiresApproval,
            attendance.IsApproved,
            attendance.ApprovedBy,
            attendance.ApprovedAtUtc,
            attendance.ManualOverrideReason,
            attendance.Remarks,
            attendance.CreatedAtUtc);
    }
}