using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CSM.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedRolesAsync(dbContext, cancellationToken);

        await SeedSuperAdminAsync(
            dbContext,
            passwordHasher,
            cancellationToken);

        // Seed default RBAC permissions
        await SeedRolePermissionsAsync(
            dbContext,
            cancellationToken);

        await SeedGranularRolePermissionsAsync(
            dbContext,
            cancellationToken);
    }

    private static async Task SeedRolesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var roleName in AppRoles.All)
        {
            var normalizedName = roleName.ToUpperInvariant();

            var exists = await dbContext.Roles.AnyAsync(
                x => x.NormalizedName == normalizedName,
                cancellationToken);

            if (exists)
                continue;

            dbContext.Roles.Add(new Role
            {
                Name = roleName,
                NormalizedName = normalizedName,
                Description = $"{roleName} system role",
                IsSystemRole = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSuperAdminAsync(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        const string email = "admin@csm.local";
        var normalizedEmail = email.ToUpperInvariant();

        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                NormalizedEmail = normalizedEmail,
                FirstName = "System",
                LastName = "Administrator",
                PasswordHash = passwordHasher.Hash("ChangeMe123!"),
                IsActive = true,
                MustChangePassword = true
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var superAdminRole = await dbContext.Roles.SingleAsync(
            x => x.NormalizedName == AppRoles.SuperAdmin.ToUpperInvariant(),
            cancellationToken);

        var assigned = await dbContext.UserRoles.AnyAsync(
            x => x.UserId == user.Id && x.RoleId == superAdminRole.Id,
            cancellationToken);

        if (!assigned)
        {
            dbContext.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = superAdminRole.Id
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedRolePermissionsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.RolePermissions.AnyAsync(cancellationToken))
            return;

        var roles = await dbContext.Roles
            .ToDictionaryAsync(x => x.Name, cancellationToken);

        void Grant(string roleName, params Permission[] permissions)
        {
            if (!roles.TryGetValue(roleName, out var role))
                return;

            foreach (var permission in permissions.Distinct())
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    Permission = permission
                });
            }
        }

        Grant("Administrator", Enum.GetValues<Permission>());

        Grant("ProjectManager",
            Permission.UsersView,
            Permission.SitesView,
            Permission.SitesManage,
            Permission.HRView,
            Permission.FinanceView,
            Permission.FinanceApprove,
            Permission.ProcurementView,
            Permission.ProcurementManage,
            Permission.ComplianceView,
            Permission.ReportsView);

        Grant("SiteEngineer",
            Permission.SitesView,
            Permission.ProcurementView,
            Permission.ComplianceView,
            Permission.ReportsView);

        Grant("StoreKeeper",
            Permission.ProcurementView,
            Permission.ProcurementManage);

        Grant("HROfficer",
            Permission.HRView,
            Permission.HRManage,
            Permission.ReportsView);

        Grant("FinanceOfficer",
            Permission.FinanceView,
            Permission.FinanceApprove,
            Permission.ReportsView);

        Grant("SafetyOfficer",
            Permission.ComplianceView,
            Permission.ComplianceManage);

        Grant("Viewer",
            Permission.UsersView,
            Permission.SitesView,
            Permission.ReportsView);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedGranularRolePermissionsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .ToDictionaryAsync(x => x.Name, cancellationToken);

        void Grant(
            string roleName,
            params GranularPermission[] permissions)
        {
            if (!roles.TryGetValue(roleName, out var role))
                return;

            foreach (var permission in permissions.Distinct())
            {
                var exists = dbContext.RoleGranularPermissions.Any(
                    x =>
                        x.RoleId == role.Id &&
                        x.Permission == permission);

                if (exists)
                    continue;

                dbContext.RoleGranularPermissions.Add(
                    new RoleGranularPermission
                    {
                        RoleId = role.Id,
                        Permission = permission
                    });
            }
        }

        Grant(
            AppRoles.SuperAdmin,
            Enum.GetValues<GranularPermission>());


        Grant(
            AppRoles.CompanyAdmin,
            Enum.GetValues<GranularPermission>());

        Grant(
            AppRoles.SiteManager,
            GranularPermission.Sites_View,
            GranularPermission.Projects_View,
            GranularPermission.Projects_Manage,
            GranularPermission.Projects_DailyProgress_View,
            GranularPermission.Projects_DailyProgress_Create,
            GranularPermission.HRM_Employee_View,
            GranularPermission.HRM_Payroll_View,
            GranularPermission.HRM_Attendance_View,
            GranularPermission.HRM_Attendance_Submit,
            GranularPermission.HRM_Leave_View,
            GranularPermission.HRM_Leave_Submit,
            GranularPermission.HRM_Leave_Approve,
            GranularPermission.Finance_Budget_View,
            GranularPermission.Finance_Expense_View,
            GranularPermission.Finance_Expense_Submit,
            GranularPermission.Finance_Invoice_View,
            GranularPermission.Finance_Payment_View,
            GranularPermission.Finance_PettyCash_View,
            GranularPermission.Procurement_MaterialRequest_View,
            GranularPermission.Procurement_MaterialRequest_Submit,
            GranularPermission.Procurement_PurchaseOrder_View,
            GranularPermission.Procurement_PurchaseOrder_Manage,
            GranularPermission.Procurement_Inventory_View,
            GranularPermission.Procurement_Inventory_Adjust,
            GranularPermission.Compliance_View,
            GranularPermission.Compliance_Manage,
            GranularPermission.Notifications_View);

        Grant(
            AppRoles.Accountant,
            GranularPermission.Sites_View,
            GranularPermission.Projects_View,
            GranularPermission.HRM_Payroll_View,
            GranularPermission.HRM_Attendance_View,
            GranularPermission.HRM_Leave_View,
            GranularPermission.Finance_Budget_View,
            GranularPermission.Finance_Budget_Manage,
            GranularPermission.Finance_Expense_View,
            GranularPermission.Finance_Expense_Approve,
            GranularPermission.Finance_Invoice_View,
            GranularPermission.Finance_Payment_View,
            GranularPermission.Finance_PettyCash_View,
            GranularPermission.Finance_PettyCash_Manage,
            GranularPermission.Finance_Payment_Manage,

            GranularPermission.Procurement_Inventory_View,
            GranularPermission.Compliance_View,
            GranularPermission.Notifications_View,
            GranularPermission.Reports_FinancialSummary_View,
            GranularPermission.Reports_Export);

        Grant(
            AppRoles.HROfficer,
            GranularPermission.HRM_Employee_View,
            GranularPermission.HRM_Employee_Manage,

            GranularPermission.HRM_Contractor_View,
            GranularPermission.HRM_Contractor_Manage,

            GranularPermission.HRM_Payroll_View,
            GranularPermission.HRM_Payroll_Approve,

            GranularPermission.HRM_Attendance_View,
            GranularPermission.HRM_Attendance_Submit,

            GranularPermission.HRM_Shift_View,
            GranularPermission.HRM_Shift_Submit,

            GranularPermission.HRM_Leave_View,
            GranularPermission.HRM_Leave_Submit,
            GranularPermission.HRM_Leave_Approve,

            GranularPermission.HRM_SiteAssignment_View,
            GranularPermission.Sites_View,
            GranularPermission.HRM_SiteAssignment_Manage,

            GranularPermission.Compliance_View,
            GranularPermission.Notifications_View,
            GranularPermission.Reports_View);

        Grant(
            AppRoles.Supervisor,
            GranularPermission.Sites_View,
            GranularPermission.Projects_View,
            GranularPermission.Projects_DailyProgress_View,
            GranularPermission.Projects_DailyProgress_Create,
            GranularPermission.HRM_Employee_View,
            GranularPermission.HRM_Payroll_View,
            GranularPermission.HRM_Attendance_View,
            GranularPermission.HRM_Attendance_Submit,
            GranularPermission.HRM_Leave_View,
            GranularPermission.HRM_Leave_Submit,

            GranularPermission.Procurement_Inventory_View,
            GranularPermission.Compliance_View,
            GranularPermission.Notifications_View);

        Grant(
            AppRoles.Employee,
            GranularPermission.HRM_Employee_View,
            GranularPermission.HRM_Payroll_View,
            GranularPermission.HRM_Attendance_View,
            GranularPermission.HRM_Attendance_Submit,
            GranularPermission.HRM_Leave_View,
            GranularPermission.HRM_Leave_Submit,
            GranularPermission.Notifications_View);

        var reportingPermissions = new[]
        {
            GranularPermission.Reports_View,
            GranularPermission.Reports_FinancialSummary_View,
            GranularPermission.Reports_Export
        };

        var reportingRoles = new[]
        {
            AppRoles.SuperAdmin,
            AppRoles.CompanyAdmin,
            AppRoles.Accountant
        };

        var staleReportingPermissions = await dbContext.RoleGranularPermissions
            .Include(x => x.Role)
            .Where(x =>
                reportingPermissions.Contains(x.Permission) &&
                !reportingRoles.Contains(x.Role.Name))
            .ToListAsync(cancellationToken);

        dbContext.RoleGranularPermissions.RemoveRange(staleReportingPermissions);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}