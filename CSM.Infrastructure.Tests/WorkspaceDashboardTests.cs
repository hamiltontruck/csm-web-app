using System.Text.Json;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class WorkspaceDashboardTests
{
    [Fact]
    public async Task Supervisor_only_receives_assigned_site_projects_and_stock_without_financial_payload()
    {
        using var f = new Fixture("Supervisor", GranularPermission.Projects_View, GranularPermission.Procurement_Inventory_View);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Equal(1, result.ActiveProjects);
        Assert.Equal(25m, result.ProjectProgress);
        Assert.Equal(1, result.OpenDelays);
        Assert.Equal(1, result.LowStockItems);
        Assert.Null(result.TotalBudget);
        Assert.Null(result.TotalExpenses);
        Assert.Null(result.TotalEmployees);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("TotalBudget", json);
        Assert.DoesNotContain("TotalExpenses", json);
        Assert.DoesNotContain("BudgetUtilization", json);
    }

    [Fact]
    public async Task Unassigned_or_revoked_site_does_not_fall_back_to_company_totals()
    {
        using var f = new Fixture("Supervisor", GranularPermission.Projects_View, GranularPermission.Procurement_Inventory_View);
        f.Db.UserSiteAccesses.Single().IsActive = false;
        f.Db.SaveChanges(true);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Equal(0, result.ActiveProjects);
        Assert.Equal(0, result.LowStockItems);
        Assert.Equal(0, result.ProjectProgress);
    }

    [Fact]
    public async Task Revoked_permission_removes_only_its_card()
    {
        using var f = new Fixture("Supervisor", GranularPermission.Projects_View, GranularPermission.Procurement_Inventory_View);
        f.Db.RoleGranularPermissions.Remove(f.Db.RoleGranularPermissions.Single(x => x.Permission == GranularPermission.Procurement_Inventory_View));
        f.Db.SaveChanges(true);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Equal(1, result.ActiveProjects);
        Assert.Null(result.LowStockItems);
    }

    [Theory]
    [InlineData("Employee")]
    [InlineData("Supervisor")]
    public async Task Finance_permission_does_not_bypass_finance_reader_roles(string role)
    {
        using var f = new Fixture(role, GranularPermission.Finance_Expense_View, GranularPermission.Reports_View, GranularPermission.Reports_FinancialSummary_View);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Null(result.TotalExpenses);
        Assert.Null(result.BudgetUtilization);
    }

    [Fact]
    public async Task Accountant_budget_and_expenses_are_independent_and_company_scoped()
    {
        using var f = new Fixture("Accountant", GranularPermission.Finance_Budget_View);
        var service = new WorkspaceDashboardService(f.Db);
        var first = await service.GetAsync(f.Actor.Id);
        Assert.Equal(100m, first.TotalBudget);
        Assert.Null(first.TotalExpenses);
        Assert.Null(first.BudgetUtilization);
        f.Grant(GranularPermission.Finance_Expense_View);
        var second = await service.GetAsync(f.Actor.Id);
        Assert.Equal(30m, second.TotalExpenses);
        Assert.Null(second.BudgetUtilization);
        f.Grant(GranularPermission.Reports_View, GranularPermission.Reports_FinancialSummary_View);
        Assert.Equal(30m, (await service.GetAsync(f.Actor.Id)).BudgetUtilization);
    }

    [Fact]
    public async Task Supervisor_HR_counts_use_dated_assignments_and_do_not_expose_other_sites()
    {
        using var f = new Fixture("Supervisor", GranularPermission.HRM_Attendance_View, GranularPermission.HRM_Leave_View);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Equal(1, result.AttendanceToday);
        Assert.Equal(1, result.PendingLeave);
        Assert.Equal(1, result.SubmittedTimesheets);
        Assert.Null(result.TotalEmployees);
    }

    [Fact]
    public async Task HR_officer_sees_company_HR_counts_but_no_finance()
    {
        using var f = new Fixture("HrOfficer", GranularPermission.HRM_Attendance_View, GranularPermission.HRM_Leave_View, GranularPermission.HRM_Employee_View);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Equal(2, result.AttendanceToday);
        Assert.Equal(2, result.PendingLeave);
        Assert.Equal(2, result.SubmittedTimesheets);
        Assert.Equal(2, result.TotalEmployees);
        Assert.Null(result.TotalBudget);
    }

    [Fact]
    public async Task Company_admin_sees_authorized_company_counts_but_cannot_select_another_company()
    {
        using var f = new Fixture("CompanyAdmin", GranularPermission.Projects_View);
        var service = new WorkspaceDashboardService(f.Db);
        Assert.Equal(2, (await service.GetAsync(f.Actor.Id)).ActiveProjects);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(f.Actor.Id, f.OtherCompany));
    }

    [Fact]
    public async Task Companyless_superadmin_must_select_existing_company()
    {
        using var f = new Fixture("SuperAdmin", GranularPermission.Projects_View);
        f.Actor.CompanyId = null; f.Db.SaveChanges(true);
        var service = new WorkspaceDashboardService(f.Db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(f.Actor.Id));
        Assert.Equal(2, (await service.GetAsync(f.Actor.Id, f.Company)).ActiveProjects);
        Assert.Equal(1, (await service.GetAsync(f.Actor.Id, f.OtherCompany)).ActiveProjects);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(f.Actor.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Disabled_or_deleted_actor_is_rejected_even_with_grants()
    {
        using var f = new Fixture("CompanyAdmin", GranularPermission.Projects_View);
        f.Actor.IsActive = false; f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id));
        f.Actor.IsActive = true; f.Actor.IsDeleted = true; f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id));
    }

    [Fact]
    public async Task Deleted_or_cross_company_site_grant_is_not_usable()
    {
        using var f = new Fixture("Supervisor", GranularPermission.Projects_View);
        f.Db.ConstructionSites.Single(x => x.Id == f.Site).IsDeleted = true;
        f.Db.UserSiteAccesses.Add(new() { UserId = f.Actor.Id, CompanyId = f.Company, SiteId = f.ForeignSite, IsActive = true });
        f.Db.SaveChanges(true);
        Assert.Equal(0, (await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id)).ActiveProjects);
    }

    [Theory]
    [InlineData("Storekeeper")]
    [InlineData("ProcurementOfficer")]
    [InlineData("Administrator")]
    public async Task Company_stock_readers_do_not_require_project_site_assignments(string role)
    {
        using var f = new Fixture(role, GranularPermission.Procurement_Inventory_View);
        f.Db.UserSiteAccesses.RemoveRange(f.Db.UserSiteAccesses);
        f.Db.SaveChanges(true);
        Assert.Equal(2, (await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id)).LowStockItems);
    }

    [Fact]
    public async Task Supervisor_stock_excludes_inactive_sites()
    {
        using var f = new Fixture("Supervisor", GranularPermission.Procurement_Inventory_View);
        f.Db.ConstructionSites.Single(x => x.Id == f.Site).IsActive = false;
        f.Db.SaveChanges(true);
        Assert.Equal(0, (await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id)).LowStockItems);
    }

    [Fact]
    public async Task Shared_employee_period_is_not_attributed_to_one_site()
    {
        using var f = new Fixture("Supervisor", GranularPermission.HRM_Leave_View);
        var employee = f.Db.SiteAssignments.Single(x => x.ConstructionSiteId == f.Site).EmployeeId;
        f.Db.SiteAssignments.Add(new() { EmployeeId = employee, CompanyId = f.Company, ConstructionSiteId = f.OtherSite, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) });
        f.Db.SaveChanges(true);
        var result = await new WorkspaceDashboardService(f.Db).GetAsync(f.Actor.Id);
        Assert.Equal(0, result.PendingLeave);
        Assert.Equal(0, result.SubmittedTimesheets);
    }

    [Fact]
    public async Task PostgreSQL_executes_scoped_and_financial_summary_queries()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "dashboard_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "DASH", Name = "Dashboard QA" };
            var role = new Role { Name = "Supervisor", NormalizedName = "SUPERVISOR", GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() };
            var user = new User { Company = company, IsActive = true, Email = "qa@example.test", NormalizedEmail = "QA@EXAMPLE.TEST", UserRoles = [new() { Role = role }] };
            db.Users.Add(user);
            db.SaveChanges(true);
            var service = new WorkspaceDashboardService(db);
            var supervisor = await service.GetAsync(user.Id);
            Assert.Equal(0, supervisor.LowStockItems);
            Assert.Equal(0, supervisor.SubmittedTimesheets);
            Assert.Null(supervisor.TotalExpenses);
            role.Name = "SiteManager";
            db.SaveChanges(true);
            var manager = await service.GetAsync(user.Id);
            Assert.Equal(0, manager.TotalExpenses);
            Assert.Equal(0, manager.BudgetUtilization);
            Assert.Equal(0, manager.OpenSafetyIncidents);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Guid Company = Guid.NewGuid(), OtherCompany = Guid.NewGuid(), Site = Guid.NewGuid(), OtherSite = Guid.NewGuid(), ForeignSite = Guid.NewGuid();
        public User Actor;
        private Role Role;
        public Fixture(string role, params GranularPermission[] permissions)
        {
            Role = new() { Name = role };
            Actor = new() { CompanyId = Company, IsActive = true, UserRoles = [new() { Role = Role }] };
            Db.Users.Add(Actor);
            Db.Companies.AddRange(new Company { Id = Company, Name = "Own", TimeZone = "UTC" }, new Company { Id = OtherCompany, Name = "Foreign", TimeZone = "UTC" });
            Db.ConstructionSites.AddRange(new ConstructionSite { Id = Site, CompanyId = Company, Name = "Assigned" }, new ConstructionSite { Id = OtherSite, CompanyId = Company, Name = "Unassigned" }, new ConstructionSite { Id = ForeignSite, CompanyId = OtherCompany, Name = "Foreign" });
            Db.UserSiteAccesses.Add(new() { UserId = Actor.Id, CompanyId = Company, SiteId = Site, IsActive = true });
            foreach (var (site, company, progress) in new[] { (Site, Company, 25m), (OtherSite, Company, 75m), (ForeignSite, OtherCompany, 100m) })
            {
                var project = new Project { CompanyId = company, ConstructionSiteId = site, ProgressPercentage = progress, Status = ProjectStatus.Active };
                Db.Projects.Add(project);
                Db.ProjectDelays.Add(new() { CompanyId = company, ProjectId = project.Id, Project = project });
                Db.StockItems.Add(new() { CompanyId = company, ConstructionSiteId = site, QuantityOnHand = 0, ReorderLevel = 1 });
                var employee = new Employee { CompanyId = company, FirstName = "Worker", EmployeeNumber = Guid.NewGuid().ToString() };
                Db.Employees.Add(employee);
                Db.SiteAssignments.Add(new() { CompanyId = company, EmployeeId = employee.Id, ConstructionSiteId = site, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10) });
                Db.Attendances.Add(new() { CompanyId = company, EmployeeId = employee.Id, ConstructionSiteId = site, AttendanceDate = DateOnly.FromDateTime(DateTime.UtcNow) });
                Db.LeaveRequests.Add(new() { CompanyId = company, EmployeeId = employee.Id, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = LeaveStatus.Pending });
                Db.Timesheets.Add(new() { CompanyId = company, EmployeeId = employee.Id, PeriodStartDate = DateOnly.FromDateTime(DateTime.UtcNow), PeriodEndDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = TimesheetStatus.Submitted });
            }
            Db.Budgets.AddRange(new Budget { CompanyId = Company, TotalAmount = 100 }, new Budget { CompanyId = OtherCompany, TotalAmount = 999 });
            Db.Expenses.AddRange(new Expense { CompanyId = Company, Amount = 30 }, new Expense { CompanyId = OtherCompany, Amount = 999 });
            Grant(permissions);
        }
        public void Grant(params GranularPermission[] permissions)
        {
            foreach (var permission in permissions) Db.RoleGranularPermissions.Add(new() { RoleId = Role.Id, Role = Role, Permission = permission });
            Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
}
