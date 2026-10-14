using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.SiteAssignments;
using CSM.Application.HRM.SiteAssignments.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class SiteAssignmentService : ISiteAssignmentService
{
    private readonly ApplicationDbContext _dbContext;

    public SiteAssignmentService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<SiteAssignmentResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? employeeId = null,
        Guid? constructionSiteId = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<SiteAssignment> query =
            _dbContext.SiteAssignments
                .AsNoTracking()
                .Include(x => x.Employee)
                .Include(x => x.ConstructionSite);

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

        var assignments = await query
            .OrderByDescending(x => x.StartDate)
            .ThenBy(x => x.Employee.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return assignments
            .Select(Map)
            .ToArray();
    }

    public async Task<SiteAssignmentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var assignment = await GetAssignmentAsync(
            assignmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            assignment.CompanyId);

        return Map(assignment);
    }

    public async Task<SiteAssignmentResponse> CreateAsync(
        Guid currentUserId,
        CreateSiteAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        ValidateDates(
            request.StartDate,
            request.EndDate);

        var employee = await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.Id == request.EmployeeId,
                cancellationToken)
            ?? throw new SiteAssignmentManagementException(
                "Employee was not found.");

        if (employee.CompanyId != companyId)
        {
            throw new SiteAssignmentManagementException(
                "Employee does not belong to your company.");
        }

        EnsureEmployeeCanBeAssigned(employee);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new SiteAssignmentManagementException(
                "Construction site was not found.");

        if (site.CompanyId != companyId)
        {
            throw new SiteAssignmentManagementException(
                "Construction site does not belong to your company.");
        }

        await EnsureNoOverlapAsync(
            employee.Id,
            site.Id,
            request.StartDate,
            request.EndDate,
            null,
            cancellationToken);

        if (request.IsPrimaryAssignment)
        {
            await EnsureNoPrimaryOverlapAsync(
                employee.Id,
                request.StartDate,
                request.EndDate,
                null,
                cancellationToken);
        }

        var assignment = new SiteAssignment
        {
            CompanyId = companyId,
            EmployeeId = employee.Id,
            ConstructionSiteId = site.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RoleAtSite = Clean(request.RoleAtSite),
            IsPrimaryAssignment =
                request.IsPrimaryAssignment,
            Remarks = Clean(request.Remarks),
            Employee = employee,
            ConstructionSite = site
        };

        _dbContext.SiteAssignments.Add(assignment);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(assignment);
    }

    public async Task<SiteAssignmentResponse> UpdateAsync(
        Guid currentUserId,
        Guid assignmentId,
        UpdateSiteAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var assignment = await GetAssignmentAsync(
            assignmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            assignment.CompanyId);

        EnsureEmployeeCanBeAssigned(
            assignment.Employee);

        ValidateDates(
            request.StartDate,
            request.EndDate);

        await EnsureNoOverlapAsync(
            assignment.EmployeeId,
            assignment.ConstructionSiteId,
            request.StartDate,
            request.EndDate,
            assignment.Id,
            cancellationToken);

        if (request.IsPrimaryAssignment)
        {
            await EnsureNoPrimaryOverlapAsync(
                assignment.EmployeeId,
                request.StartDate,
                request.EndDate,
                assignment.Id,
                cancellationToken);
        }

        assignment.StartDate = request.StartDate;
        assignment.EndDate = request.EndDate;
        assignment.RoleAtSite =
            Clean(request.RoleAtSite);
        assignment.IsPrimaryAssignment =
            request.IsPrimaryAssignment;
        assignment.Remarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(assignment);
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
            throw new SiteAssignmentManagementException(
                "Current user was not found or is inactive.");
        }

        return actor;
    }

    private async Task<SiteAssignment> GetAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SiteAssignments
            .Include(x => x.Employee)
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == assignmentId,
                cancellationToken)
            ?? throw new SiteAssignmentManagementException(
                "Site assignment was not found.");
    }

    private async Task EnsureNoOverlapAsync(
        Guid employeeId,
        Guid constructionSiteId,
        DateOnly startDate,
        DateOnly? endDate,
        Guid? excludedAssignmentId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SiteAssignments
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.ConstructionSiteId ==
                    constructionSiteId);

        if (excludedAssignmentId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludedAssignmentId.Value);
        }

        var overlaps = await query.AnyAsync(
            x =>
                (!x.EndDate.HasValue ||
                 x.EndDate.Value >= startDate) &&
                (!endDate.HasValue ||
                 x.StartDate <= endDate.Value),
            cancellationToken);

        if (overlaps)
        {
            throw new SiteAssignmentManagementException(
                "The employee already has an overlapping assignment to this construction site.");
        }
    }

    private async Task EnsureNoPrimaryOverlapAsync(
        Guid employeeId,
        DateOnly startDate,
        DateOnly? endDate,
        Guid? excludedAssignmentId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SiteAssignments
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.IsPrimaryAssignment);

        if (excludedAssignmentId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludedAssignmentId.Value);
        }

        var overlaps = await query.AnyAsync(
            x =>
                (!x.EndDate.HasValue ||
                 x.EndDate.Value >= startDate) &&
                (!endDate.HasValue ||
                 x.StartDate <= endDate.Value),
            cancellationToken);

        if (overlaps)
        {
            throw new SiteAssignmentManagementException(
                "The employee already has an overlapping primary site assignment.");
        }
    }

    private static void EnsureEmployeeCanBeAssigned(
        Employee employee)
    {
        if (employee.Status ==
            EmployeeStatus.Terminated)
        {
            throw new SiteAssignmentManagementException(
                "A terminated employee cannot be assigned to a construction site.");
        }

        if (employee.Status ==
            EmployeeStatus.Inactive)
        {
            throw new SiteAssignmentManagementException(
                "An inactive employee cannot be assigned to a construction site.");
        }
    }

    private static void ValidateDates(
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (endDate.HasValue &&
            endDate.Value < startDate)
        {
            throw new SiteAssignmentManagementException(
                "Assignment end date cannot be earlier than start date.");
        }
    }

    private static string? Clean(string? value)
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

        throw new SiteAssignmentManagementException(
            "You are not authorized to access site assignments.");
    }

    private static void EnsureCanModify(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new SiteAssignmentManagementException(
            "You are not authorized to create or modify site assignments.");
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new SiteAssignmentManagementException(
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
            throw new SiteAssignmentManagementException(
                "You cannot access site assignments outside your company.");
        }
    }

    private static bool IsSuperAdmin(User actor)
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

    private static SiteAssignmentResponse Map(
        SiteAssignment assignment)
    {
        var today = DateOnly.FromDateTime(
            DateTime.UtcNow);

        var isActive =
            assignment.StartDate <= today &&
            (!assignment.EndDate.HasValue ||
             assignment.EndDate.Value >= today);

        var employeeName = string.Join(
            " ",
            new[]
            {
                assignment.Employee.FirstName,
                assignment.Employee.MiddleName,
                assignment.Employee.LastName
            }
            .Where(x =>
                !string.IsNullOrWhiteSpace(x)));

        return new SiteAssignmentResponse(
            assignment.Id,
            assignment.CompanyId,
            assignment.EmployeeId,
            assignment.Employee.EmployeeNumber,
            employeeName,
            assignment.ConstructionSiteId,
            assignment.ConstructionSite.Name,
            assignment.StartDate,
            assignment.EndDate,
            assignment.RoleAtSite,
            assignment.IsPrimaryAssignment,
            assignment.Remarks,
            isActive,
            assignment.CreatedAtUtc);
    }
}