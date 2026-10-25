using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CSM.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedRolesAsync(dbContext, cancellationToken);

        await SeedSuperAdminAsync(
            dbContext,
            passwordHasher,
            configuration,
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
        var roleNames = AppRoles.All
            .Concat(new[] { AppRoles.Storekeeper, AppRoles.ProcurementOfficer })
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames)
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
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var superAdminRole = await dbContext.Roles.SingleAsync(
            x => x.NormalizedName == AppRoles.SuperAdmin.ToUpperInvariant(),
            cancellationToken);

        // Do not create or reset a privileged account from a built-in password.
        // Existing installations keep their configured super admin.
        var existingSuperAdmin = await dbContext.UserRoles.AnyAsync(
            x => x.RoleId == superAdminRole.Id,
            cancellationToken);

        if (existingSuperAdmin)
            return;

        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No super administrator exists. Set BootstrapAdmin:Email and " +
                "BootstrapAdmin:Password using environment variables or a " +
                "secret store before starting the API for the first time.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new InvalidOperationException(
                "BootstrapAdmin:Email must be a valid email address.");

        ValidateBootstrapPassword(password);
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
                PasswordHash = passwordHasher.Hash(password),
                IsActive = true,
                MustChangePassword = true
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

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

    private static void ValidateBootstrapPassword(string password)
    {
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Password must be at least 12 characters and " +
                "include uppercase, lowercase, numeric, and special characters.");
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
                role = roles.Values.FirstOrDefault(x =>
                    string.Equals(x.Name, roleName, StringComparison.OrdinalIgnoreCase));
            if (role is null)
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

        // Match the roles allowed by AttendanceService.EnsureCanApprove.
        Grant(AppRoles.SiteManager, GranularPermission.HRM_Attendance_Approve);

        // Inventory service roles need site lookup plus inventory view/adjust permissions.
        Grant(
            AppRoles.Storekeeper,
            GranularPermission.Sites_View,
            GranularPermission.Procurement_Inventory_View,
            GranularPermission.Procurement_Inventory_Adjust);

        Grant(
            AppRoles.ProcurementOfficer,
            GranularPermission.Sites_View,
            GranularPermission.Procurement_Inventory_View,
            GranularPermission.Procurement_Inventory_Adjust);

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
