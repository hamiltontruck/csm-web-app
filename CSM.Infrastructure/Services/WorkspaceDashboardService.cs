using CSM.Application.Dashboard;
using CSM.Application.Dashboard.Dtos;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

/// <summary>Read-only card summaries. Authority is loaded from the database, never from requested card names.</summary>
public sealed class WorkspaceDashboardService(ApplicationDbContext db) : IWorkspaceDashboardService
{
    public async Task<WorkspaceDashboardResponse> GetAsync(Guid actorId, Guid? selectedCompanyId = null, CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Your account is unavailable. Please sign in again.");
        var activeRoles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role).ToArray();
        var roles = activeRoles.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var permissions = activeRoles.SelectMany(x => x.GranularPermissions).Select(x => x.Permission).ToHashSet();
        bool IsIn(params string[] names) => names.Any(roles.Contains);
        bool Has(GranularPermission permission) => permissions.Contains(permission);
        var superAdmin = IsIn("SuperAdmin");
        if (selectedCompanyId.HasValue && !superAdmin)
            throw new UnauthorizedAccessException("You cannot select another company's dashboard.");
        var companyId = selectedCompanyId ?? actor.CompanyId;
        if (!companyId.HasValue || companyId == Guid.Empty)
            throw new UnauthorizedAccessException("A company must be assigned or selected to view this dashboard.");
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == companyId && !x.IsDeleted && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Selected company was not found.");

        // Match project/stock scope: only administrators have unrestricted company sites.
        var allSites = IsIn("SuperAdmin", "CompanyAdmin");
        var sites = db.ConstructionSites.AsNoTracking().Where(x => x.CompanyId == company.Id && !x.IsDeleted &&
            (allSites || db.UserSiteAccesses.Any(a => a.UserId == actorId && a.CompanyId == company.Id && a.SiteId == x.Id && a.IsActive && !a.IsDeleted)))
            .Select(x => x.Id);
        var hrReader = IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "Supervisor", "HrOfficer", "HROfficer");
        var hrCompany = IsIn("SuperAdmin", "CompanyAdmin", "HrOfficer", "HROfficer");
        var financeReader = IsIn("SuperAdmin", "CompanyAdmin", "Accountant", "SiteManager");
        var financeCompany = IsIn("SuperAdmin", "CompanyAdmin", "Accountant");
        var todayUtc = DateTime.UtcNow;
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(company.TimeZone, out var configured) ? configured : TimeZoneInfo.Utc;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(todayUtc, zone));
        var result = new WorkspaceDashboardResponse { GeneratedAtUtc = todayUtc, Today = today, CurrencyCode = company.BaseCurrencyCode };

        if (Has(GranularPermission.Projects_View))
        {
            var projects = db.Projects.AsNoTracking().Where(x => x.CompanyId == company.Id && !x.IsDeleted && sites.Contains(x.ConstructionSiteId));
            result.ActiveProjects = await projects.CountAsync(x => x.Status != ProjectStatus.Completed && x.Status != ProjectStatus.Cancelled && x.Status != ProjectStatus.Closed, cancellationToken);
            result.CompletedProjects = await projects.CountAsync(x => x.Status == ProjectStatus.Completed, cancellationToken);
            result.ProjectProgress = Math.Round(await projects.AverageAsync(x => (decimal?)x.ProgressPercentage, cancellationToken) ?? 0, 2);
            result.OpenDelays = await db.ProjectDelays.AsNoTracking().CountAsync(x => x.CompanyId == company.Id && !x.IsDeleted && !x.IsResolved && projects.Select(p => p.Id).Contains(x.ProjectId), cancellationToken);
        }
        if (Has(GranularPermission.Procurement_Inventory_View) && IsIn("SuperAdmin", "Administrator", "Supervisor", "Storekeeper", "ProcurementOfficer"))
            result.LowStockItems = await db.StockItems.AsNoTracking().CountAsync(x => x.CompanyId == company.Id && !x.IsDeleted && sites.Contains(x.ConstructionSiteId) && x.QuantityOnHand <= x.ReorderLevel, cancellationToken);
        if (Has(GranularPermission.Compliance_View) && IsIn("SuperAdmin", "CompanyAdmin", "SiteManager"))
            result.OpenSafetyIncidents = await db.SafetyIncidents.AsNoTracking().CountAsync(x => x.CompanyId == company.Id && !x.IsDeleted && sites.Contains(x.ConstructionSiteId) && x.Status != SafetyIncidentStatus.Resolved, cancellationToken);
        if (Has(GranularPermission.HRM_Employee_View) && hrCompany)
            result.TotalEmployees = await db.Employees.AsNoTracking().CountAsync(x => x.CompanyId == company.Id && !x.IsDeleted, cancellationToken);
        var employees = db.Employees.AsNoTracking().Where(x => x.CompanyId == company.Id && !x.IsDeleted).Select(x => x.Id);
        if (Has(GranularPermission.HRM_Attendance_View) && hrReader)
        {
            var attendance = db.Attendances.AsNoTracking().Where(x => x.CompanyId == company.Id && !x.IsDeleted && employees.Contains(x.EmployeeId) && (hrCompany || sites.Contains(x.ConstructionSiteId)));
            result.AttendanceToday = await attendance.CountAsync(x => x.AttendanceDate == today, cancellationToken);
            result.PendingAttendance = await attendance.CountAsync(x => x.RequiresApproval && !x.IsApproved, cancellationToken);
        }
        // Employee-level summaries cannot be split between sites. Require an assignment
        // covering the whole period, and exclude periods also assigned to an inaccessible site.
        var assignments = db.SiteAssignments.AsNoTracking().Where(a => a.CompanyId == company.Id && !a.IsDeleted);
        if (Has(GranularPermission.HRM_Leave_View) && hrReader)
            result.PendingLeave = await db.LeaveRequests.AsNoTracking().CountAsync(x => x.CompanyId == company.Id && !x.IsDeleted && employees.Contains(x.EmployeeId) && x.Status == LeaveStatus.Pending &&
                (hrCompany || (assignments.Any(a => a.EmployeeId == x.EmployeeId && sites.Contains(a.ConstructionSiteId) && a.StartDate <= x.StartDate && (!a.EndDate.HasValue || a.EndDate >= x.EndDate)) &&
                    !assignments.Any(a => a.EmployeeId == x.EmployeeId && !sites.Contains(a.ConstructionSiteId) && a.StartDate <= x.EndDate && (!a.EndDate.HasValue || a.EndDate >= x.StartDate)))), cancellationToken);
        // Timesheets have no granular permission in the current contract; retain its reader roles.
        if (hrReader)
            result.SubmittedTimesheets = await db.Timesheets.AsNoTracking().CountAsync(x => x.CompanyId == company.Id && !x.IsDeleted && employees.Contains(x.EmployeeId) && x.Status == TimesheetStatus.Submitted &&
                (hrCompany || (assignments.Any(a => a.EmployeeId == x.EmployeeId && sites.Contains(a.ConstructionSiteId) && a.StartDate <= x.PeriodStartDate && (!a.EndDate.HasValue || a.EndDate >= x.PeriodEndDate)) &&
                    !assignments.Any(a => a.EmployeeId == x.EmployeeId && !sites.Contains(a.ConstructionSiteId) && a.StartDate <= x.PeriodEndDate && (!a.EndDate.HasValue || a.EndDate >= x.PeriodStartDate)))), cancellationToken);
        if (Has(GranularPermission.Finance_Budget_View))
            result.TotalBudget = await db.Budgets.AsNoTracking().Where(x => x.CompanyId == company.Id && !x.IsDeleted && x.CurrencyCode == result.CurrencyCode &&
                (financeCompany || (x.ConstructionSiteId.HasValue && sites.Contains(x.ConstructionSiteId.Value))))
                .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;
        if (financeReader && Has(GranularPermission.Finance_Expense_View))
            result.TotalExpenses = await db.Expenses.AsNoTracking().Where(x => x.CompanyId == company.Id && !x.IsDeleted && x.CurrencyCode == result.CurrencyCode &&
                (financeCompany || (x.ConstructionSiteId.HasValue && sites.Contains(x.ConstructionSiteId.Value))))
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        if (result.TotalBudget.HasValue && result.TotalExpenses.HasValue && Has(GranularPermission.Reports_View) && Has(GranularPermission.Reports_FinancialSummary_View))
            result.BudgetUtilization = result.TotalBudget == 0 ? 0 : Math.Round(result.TotalExpenses.Value / result.TotalBudget.Value * 100, 2);
        return result;
    }
}
