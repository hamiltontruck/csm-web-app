using System.Reflection;
using System.Security.Claims;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;
public sealed class BackendActionPermissionTests
{
    public static IEnumerable<object[]> Actions => new object[][]
    {
        new object[] { "AttendancesController", "GetAll", GranularPermission.HRM_Attendance_View, GranularPermission.HRM_Attendance_View },
        new object[] { "AttendancesController", "GetById", GranularPermission.HRM_Attendance_View, GranularPermission.HRM_Attendance_View },
        new object[] { "AttendancesController", "CheckIn", GranularPermission.HRM_Attendance_Submit, GranularPermission.HRM_Attendance_View },
        new object[] { "AttendancesController", "CheckOut", GranularPermission.HRM_Attendance_Submit, GranularPermission.HRM_Attendance_View },
        new object[] { "AttendancesController", "Approve", GranularPermission.HRM_Attendance_Approve, GranularPermission.HRM_Attendance_View },
        new object[] { "AttendancesController", "Override", GranularPermission.HRM_Attendance_Approve, GranularPermission.HRM_Attendance_View },
        new object[] { "ComplianceAlertsController", "Get", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "ComplianceDashboardController", "Get", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "ComplianceReportsController", "Summary", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "ComplianceViolationsController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "ContractorsController", "GetAll", GranularPermission.HRM_Contractor_View, GranularPermission.HRM_Contractor_View },
        new object[] { "ContractorsController", "GetById", GranularPermission.HRM_Contractor_View, GranularPermission.HRM_Contractor_View },
        new object[] { "ContractorsController", "Create", GranularPermission.HRM_Contractor_Manage, GranularPermission.HRM_Contractor_View },
        new object[] { "ContractorsController", "Update", GranularPermission.HRM_Contractor_Manage, GranularPermission.HRM_Contractor_View },
        new object[] { "DailyProgressLogsController", "GetAll", GranularPermission.Projects_DailyProgress_View, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressLogsController", "GetById", GranularPermission.Projects_DailyProgress_View, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressLogsController", "Create", GranularPermission.Projects_DailyProgress_Create, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressLogsController", "Update", GranularPermission.Projects_DailyProgress_Create, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressTaskEntriesController", "Create", GranularPermission.Projects_DailyProgress_Create, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressTaskEntriesController", "Get", GranularPermission.Projects_DailyProgress_View, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressTaskEntriesController", "AddEquipment", GranularPermission.Projects_DailyProgress_Create, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DailyProgressTaskEntriesController", "GetEquipment", GranularPermission.Projects_DailyProgress_View, GranularPermission.Projects_DailyProgress_View },
        new object[] { "DocumentCategoriesController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "DocumentCategoriesController", "GetById", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "DocumentCategoriesController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "DocumentCategoriesController", "Update", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "DocumentVersionsController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "DocumentVersionsController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "DocumentVersionsController", "Download", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "DocumentsController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "DocumentsController", "GetById", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "DocumentsController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "DocumentsController", "Update", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "DocumentsController", "UploadVersion", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "EmployeesController", "GetAll", GranularPermission.HRM_Employee_View, GranularPermission.HRM_Employee_View },
        new object[] { "EmployeesController", "GetById", GranularPermission.HRM_Employee_View, GranularPermission.HRM_Employee_View },
        new object[] { "EmployeesController", "Create", GranularPermission.HRM_Employee_Manage, GranularPermission.HRM_Employee_View },
        new object[] { "EmployeesController", "Update", GranularPermission.HRM_Employee_Manage, GranularPermission.HRM_Employee_View },
        new object[] { "EmployeesController", "ChangeStatus", GranularPermission.HRM_Employee_Manage, GranularPermission.HRM_Employee_View },
        new object[] { "EquipmentAssignmentsController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentAssignmentsController", "GetById", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentAssignmentsController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentAssignmentsController", "Update", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentAssignmentsController", "Release", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentController", "GetById", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentController", "Update", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentDowntimeController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentDowntimeController", "GetById", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentDowntimeController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentDowntimeController", "Update", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentDowntimeController", "Close", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentFuelLogsController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentFuelLogsController", "GetById", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentFuelLogsController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentFuelLogsController", "Update", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentHourLogsController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentHourLogsController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentMaintenanceController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentMaintenanceController", "GetById", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentMaintenanceController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentMaintenanceController", "Update", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentTimesheetsController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentTimesheetsController", "Create", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentTimesheetsController", "GetById", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentTimesheetsController", "Update", GranularPermission.Procurement_Inventory_Adjust, GranularPermission.Procurement_Inventory_View },
        new object[] { "EquipmentUtilizationController", "GetAll", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View },
        new object[] { "LeavesController", "GetEmployeeOptions", GranularPermission.HRM_Leave_Submit, GranularPermission.HRM_Leave_View },
        new object[] { "LeavesController", "GetAll", GranularPermission.HRM_Leave_View, GranularPermission.HRM_Leave_View },
        new object[] { "LeavesController", "GetById", GranularPermission.HRM_Leave_View, GranularPermission.HRM_Leave_View },
        new object[] { "LeavesController", "Create", GranularPermission.HRM_Leave_Submit, GranularPermission.HRM_Leave_View },
        new object[] { "LeavesController", "Review", GranularPermission.HRM_Leave_Approve, GranularPermission.HRM_Leave_View },
        new object[] { "LeavesController", "Cancel", GranularPermission.HRM_Leave_Approve, GranularPermission.HRM_Leave_View },
        new object[] { "NonConformanceReportController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "NonConformanceReportController", "GetById", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "NonConformanceReportController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "NonConformanceReportController", "Update", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "QualityChecklistController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "QualityChecklistController", "GetById", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "QualityChecklistController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "QualityChecklistController", "Update", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "SafetyIncidentsController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "SafetyIncidentsController", "GetById", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "SafetyIncidentsController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "SafetyIncidentsController", "Update", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "SafetyObservationController", "GetAll", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "SafetyObservationController", "GetById", GranularPermission.Compliance_View, GranularPermission.Compliance_View },
        new object[] { "SafetyObservationController", "Create", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "SafetyObservationController", "Update", GranularPermission.Compliance_Manage, GranularPermission.Compliance_View },
        new object[] { "ShiftsController", "GetAll", GranularPermission.HRM_Shift_View, GranularPermission.HRM_Shift_View },
        new object[] { "ShiftsController", "GetById", GranularPermission.HRM_Shift_View, GranularPermission.HRM_Shift_View },
        new object[] { "ShiftsController", "Create", GranularPermission.HRM_Shift_Submit, GranularPermission.HRM_Shift_View },
        new object[] { "ShiftsController", "Update", GranularPermission.HRM_Shift_Submit, GranularPermission.HRM_Shift_View },
        new object[] { "ShiftsController", "ChangeActiveStatus", GranularPermission.HRM_Shift_Submit, GranularPermission.HRM_Shift_View },
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task HTTP_action_requires_its_own_permission_even_when_all_role_checks_pass(string controllerName, string action, GranularPermission required, GranularPermission read)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var role = new Role { Name = "SuperAdmin" };
        var actor = new User { IsActive = true, UserRoles = [new() { Role = role }] };
        db.Users.Add(actor);
        if (controllerName.StartsWith("DailyProgress")) db.RoleGranularPermissions.Add(new() { Role = role, Permission = GranularPermission.Projects_View });
        if (read != required) db.RoleGranularPermissions.Add(new() { Role = role, Permission = read });
        db.SaveChanges(true);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:SecretKey"] = "test-secret-with-at-least-thirty-two-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test"
        }).Build();
        var registrations = new ServiceCollection().AddLogging();
        CSM.Infrastructure.DependencyInjection.AddInfrastructure(registrations, config);
        registrations.AddSingleton(db);
        using var services = registrations.BuildServiceProvider();
        var controller = typeof(CSM.Api.Controllers.ProjectsController).Assembly.GetTypes().Single(t => t.Name == controllerName);
        var metadata = controller.GetCustomAttributes<AuthorizeAttribute>().Concat(controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        // Deliberately satisfy every legacy role gate to isolate the missing granular grant.
        var claims = typeof(AppRoles).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(string)).Select(f => new Claim(ClaimTypes.Role, (string)f.GetValue(null)!)).ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var authorization = services.GetRequiredService<IAuthorizationService>();
        Assert.False((await authorization.AuthorizeAsync(principal, null, policy!)).Succeeded);
        db.RoleGranularPermissions.Add(new() { Role = role, Permission = required });
        db.SaveChanges(true);
        Assert.True((await authorization.AuthorizeAsync(principal, null, policy!)).Succeeded);
    }
}
