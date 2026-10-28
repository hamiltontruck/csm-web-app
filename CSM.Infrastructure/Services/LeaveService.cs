using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Leaves;
using CSM.Application.HRM.Leaves.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class LeaveService : ILeaveService
{
    private readonly ApplicationDbContext _dbContext;

    public LeaveService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<LeaveEmployeeOptionResponse>> GetEmployeeOptionsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        EnsureCanCreate(actor);
        var companyId = GetActorCompanyId(actor);

        var employees = await _dbContext.Employees.AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.Status == EmployeeStatus.Active)
            .OrderBy(x => x.EmployeeNumber).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.EmployeeNumber, x.FirstName, x.MiddleName, x.LastName })
            .ToListAsync(cancellationToken);

        return employees.Select(x => new LeaveEmployeeOptionResponse(
            x.Id, x.EmployeeNumber,
            string.Join(" ", new[] { x.FirstName, x.MiddleName, x.LastName }
                .Where(name => !string.IsNullOrWhiteSpace(name))))).ToList();
    }

    public async Task<IReadOnlyCollection<LeaveResponse>> GetAllAsync(
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

        if (fromDate.HasValue &&
            toDate.HasValue &&
            toDate.Value < fromDate.Value)
        {
            throw new LeaveManagementException(
                "To date cannot be earlier than from date.");
        }

        IQueryable<LeaveRequest> query =
            _dbContext.LeaveRequests
                .AsNoTracking()
                .Include(x => x.Employee);

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
                x => x.EndDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(
                x => x.StartDate <= toDate.Value);
        }

        var leaveRequests = await query
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return leaveRequests
            .Select(Map)
            .ToArray();
    }

    public async Task<LeaveResponse> GetByIdAsync(
        Guid currentUserId,
        Guid leaveRequestId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var leaveRequest = await GetLeaveRequestAsync(
            leaveRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            leaveRequest.CompanyId);

        return Map(leaveRequest);
    }

    public async Task<LeaveResponse> CreateAsync(
        Guid currentUserId,
        CreateLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanCreate(actor);

        var companyId = GetActorCompanyId(actor);

        ValidateDates(
            request.StartDate,
            request.EndDate);

        var leaveType = Clean(request.LeaveType);

        if (leaveType is null)
        {
            throw new LeaveManagementException(
                "Leave type is required.");
        }

        if (leaveType.Length > 50)
        {
            throw new LeaveManagementException(
                "Leave type cannot exceed 50 characters.");
        }

        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.Id == request.EmployeeId,
                cancellationToken)
            ?? throw new LeaveManagementException(
                "Employee was not found.");

        if (employee.CompanyId != companyId)
        {
            throw new LeaveManagementException(
                "Employee does not belong to your company.");
        }

        EnsureEmployeeCanRequestLeave(employee);

        var overlaps = await _dbContext.LeaveRequests
            .AnyAsync(
                x =>
                    x.EmployeeId == employee.Id &&
                    x.Status != LeaveStatus.Rejected &&
                    x.Status != LeaveStatus.Cancelled &&
                    x.StartDate <= request.EndDate &&
                    x.EndDate >= request.StartDate,
                cancellationToken);

        if (overlaps)
        {
            throw new LeaveManagementException(
                "Employee already has an overlapping active leave request.");
        }

        var leaveRequest = new LeaveRequest
        {
            CompanyId = companyId,
            EmployeeId = employee.Id,
            LeaveType = leaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfDays = CalculateNumberOfDays(
                request.StartDate,
                request.EndDate),
            Reason = Clean(request.Reason),
            Status = LeaveStatus.Pending,
            ReviewedBy = null,
            ReviewedAtUtc = null,
            ReviewRemarks = null
        };

        _dbContext.LeaveRequests.Add(leaveRequest);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        leaveRequest.Employee = employee;

        return Map(leaveRequest);
    }

    public async Task<LeaveResponse> ReviewAsync(
        Guid currentUserId,
        Guid leaveRequestId,
        ReviewLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanReview(actor);

        if (request.Status != LeaveStatus.Approved &&
            request.Status != LeaveStatus.Rejected)
        {
            throw new LeaveManagementException(
                "Review status must be Approved or Rejected.");
        }

        var leaveRequest = await GetLeaveRequestAsync(
            leaveRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            leaveRequest.CompanyId);

        if (leaveRequest.Status != LeaveStatus.Pending)
        {
            throw new LeaveManagementException(
                "Only pending leave requests can be reviewed.");
        }

        leaveRequest.Status = request.Status;
        leaveRequest.ReviewedBy = actor.Id;
        leaveRequest.ReviewedAtUtc = DateTime.UtcNow;
        leaveRequest.ReviewRemarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(leaveRequest);
    }

    public async Task<LeaveResponse> CancelAsync(
        Guid currentUserId,
        Guid leaveRequestId,
        CancelLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanCancel(actor);

        var leaveRequest = await GetLeaveRequestAsync(
            leaveRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            leaveRequest.CompanyId);

        if (leaveRequest.Status == LeaveStatus.Cancelled)
        {
            throw new LeaveManagementException(
                "Leave request has already been cancelled.");
        }

        if (leaveRequest.Status == LeaveStatus.Rejected)
        {
            throw new LeaveManagementException(
                "Rejected leave requests cannot be cancelled.");
        }

        leaveRequest.Status = LeaveStatus.Cancelled;
        leaveRequest.ReviewedBy = actor.Id;
        leaveRequest.ReviewedAtUtc = DateTime.UtcNow;
        leaveRequest.ReviewRemarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(leaveRequest);
    }

    private async Task<User> GetActorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken)
            ?? throw new LeaveManagementException(
                "Current user was not found.");

        if (!user.IsActive)
        {
            throw new LeaveManagementException(
                "Current user account is inactive.");
        }

        return user;
    }

    private async Task<LeaveRequest> GetLeaveRequestAsync(
        Guid leaveRequestId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.LeaveRequests
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(
                x => x.Id == leaveRequestId,
                cancellationToken)
            ?? throw new LeaveManagementException(
                "Leave request was not found.");
    }

    private static void EnsureCanRead(User actor)
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

        throw new LeaveManagementException(
            "You are not authorized to access leave requests.");
    }

    private static void EnsureCanCreate(User actor)
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

        throw new LeaveManagementException(
            "You are not authorized to create leave requests.");
    }

    private static void EnsureCanReview(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new LeaveManagementException(
            "You are not authorized to review leave requests.");
    }


    private static void EnsureCanCancel(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new LeaveManagementException(
            "You are not authorized to cancel leave requests.");
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
            throw new LeaveManagementException(
                "You cannot access leave requests outside your company.");
        }
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new LeaveManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static void EnsureEmployeeCanRequestLeave(
        Employee employee)
    {
        if (employee.Status != EmployeeStatus.Active)
        {
            throw new LeaveManagementException(
                "Leave can only be requested for an active employee.");
        }
    }

    private static void ValidateDates(
        DateOnly startDate,
        DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new LeaveManagementException(
                "End date cannot be earlier than start date.");
        }
    }

    private static decimal CalculateNumberOfDays(
        DateOnly startDate,
        DateOnly endDate)
    {
        return endDate.DayNumber -
               startDate.DayNumber +
               1;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool IsSuperAdmin(User actor) =>
        HasRole(actor, AppRoles.SuperAdmin);

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

    private static LeaveResponse Map(
        LeaveRequest leaveRequest)
    {
        return new LeaveResponse(
            leaveRequest.Id,
            leaveRequest.CompanyId,
            leaveRequest.EmployeeId,
            leaveRequest.Employee.EmployeeNumber,
            BuildEmployeeName(leaveRequest.Employee),
            leaveRequest.LeaveType,
            leaveRequest.StartDate,
            leaveRequest.EndDate,
            leaveRequest.NumberOfDays,
            leaveRequest.Reason,
            leaveRequest.Status,
            leaveRequest.ReviewedBy,
            leaveRequest.ReviewedAtUtc,
            leaveRequest.ReviewRemarks,
            leaveRequest.CreatedAtUtc);
    }

    private static string BuildEmployeeName(
        Employee employee)
    {
        return string.Join(
            " ",
            new[]
            {
                employee.FirstName,
                employee.MiddleName,
                employee.LastName
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
