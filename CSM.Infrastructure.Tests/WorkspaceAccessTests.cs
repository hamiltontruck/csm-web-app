using System.Reflection;
using Bunit;
using CSM.Api.Controllers;
using CSM.Web.Components;
using CSM.Web.Services;
using CSM.Web.Services.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class WorkspaceAccessTests
{
    [Theory]
    [InlineData("/hr/payroll", "HRM_Payroll_View", true)]
    [InlineData("/hr/payroll/edit/record", "HRM_Payroll_View", true)]
    [InlineData("/hr/payroll/create", "HRM_Payroll_View,HRM_Payroll_Approve", false)]
    [InlineData("/hr/payroll", "", false)]
    [InlineData("/hr/timesheets", "", true)]
    [InlineData("/hr/timesheets/edit/record", "", true)]
    [InlineData("/hr/timesheets/create", "", false)]
    [InlineData("/hr/employees", "HRM_Employee_View,HRM_Employee_Manage", false)]
    [InlineData("/hr/contractors", "HRM_Contractor_View", false)]
    [InlineData("/hr/attendance", "HRM_Attendance_View", true)]
    [InlineData("/hr/attendance/check-in", "HRM_Attendance_View,HRM_Attendance_Submit,HRM_Employee_View", false)]
    [InlineData("/hr/attendance/check-out/record", "HRM_Attendance_View,HRM_Attendance_Submit", true)]
    [InlineData("/hr/attendance/override/record", "HRM_Attendance_View,HRM_Attendance_Approve", false)]
    [InlineData("/hr/shifts/create", "HRM_Shift_View,HRM_Shift_Submit", false)]
    [InlineData("/hr/site-assignments/create", "HRM_SiteAssignment_View,HRM_SiteAssignment_Manage", false)]
    [InlineData("/hr/leave/create", "HRM_Leave_View,HRM_Leave_Submit", true)]
    [InlineData("/equipment", "Procurement_Inventory_View", false)]
    [InlineData("/equipment/list", "Procurement_Inventory_View", false)]
    [InlineData("/equipment/timesheet-sites", "Procurement_Inventory_View", false)]
    [InlineData("/compliance", "Compliance_View", false)]
    [InlineData("/compliance/documents", "Compliance_View,Compliance_Manage", false)]
    [InlineData("/materials", "Procurement_Inventory_View", false)]
    [InlineData("/purchase-orders", "Procurement_PurchaseOrder_View,Procurement_PurchaseOrder_Manage", false)]
    [InlineData("/goods-receipts", "Procurement_Inventory_View", false)]
    [InlineData("/procurement", "Procurement_Inventory_View,Procurement_MaterialRequest_View,Procurement_PurchaseOrder_View", false)]
    [InlineData("/material-requests", "Procurement_MaterialRequest_View", false)]
    [InlineData("/stock-balance", "Procurement_Inventory_View,Sites_View", true)]
    [InlineData("/stock-transfers", "Procurement_Inventory_View,Sites_View", true)]
    [InlineData("/stock-transfers/create", "Procurement_Inventory_View,Procurement_Inventory_Adjust,Sites_View", false)]
    [InlineData("/stock-movements", "Procurement_Inventory_View", false)]
    [InlineData("/stock-issues", "Procurement_Inventory_View", false)]
    [InlineData("/stock-returns", "Procurement_Inventory_View", false)]
    [InlineData("/daily-material-usage", "Procurement_Inventory_View,Sites_View", true)]
    [InlineData("/daily-material-usage/create", "Procurement_Inventory_View,Procurement_Inventory_Adjust,Sites_View", false)]
    [InlineData("/projects", "Projects_View", true)]
    [InlineData("/projects/create", "Projects_Manage", false)]
    [InlineData("/projects/p/daily-progress/create", "Projects_View,Projects_DailyProgress_Create", true)]
    [InlineData("/finance/company/invoices", "Finance_Invoice_View", false)]
    [InlineData("/finance/company/payments", "Finance_Payment_View", false)]
    [InlineData("/users", "Users_View", false)]
    [InlineData("/companies", "Users_View", false)]
    [InlineData("/reports", "Reports_View", true)]
    [InlineData("/unregistered-screen", "Reports_View", false)]
    public void Supervisor_visibility_intersects_permissions_roles_and_required_lookups(string route, string permissions, bool expected)
    {
        var access = new WorkspaceAccess(["Supervisor"], permissions.Split(',', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(expected, access.CanVisit(route));
    }

    [Theory]
    [InlineData("CompanyAdmin", "HRM_Payroll_View,HRM_Employee_View", "/hr/payroll/create", true)]
    [InlineData("CompanyAdmin", "HRM_Employee_View,HRM_Employee_Manage", "/hr/employees/create", true)]
    [InlineData("CompanyAdmin", "HRM_SiteAssignment_View", "/hr/site-assignments/create", false)]
    [InlineData("CompanyAdmin", "HRM_SiteAssignment_View,HRM_SiteAssignment_Manage", "/hr/site-assignments/create", true)]
    [InlineData("Supervisor,CompanyAdmin", "HRM_Payroll_View,HRM_Employee_View", "/hr/payroll/create", true)]
    [InlineData("Storekeeper", "Procurement_Inventory_View", "/stock-movements", true)]
    [InlineData("Supervisor", "HRM_Payroll_View", "/HR/PAYROLL/?id=x", true)]
    public void Other_roles_and_multi_role_accounts_keep_their_existing_capabilities(string roles, string permissions, string route, bool expected)
    {
        Assert.Equal(expected, new WorkspaceAccess(roles.Split(','), permissions.Split(',')).CanVisit(route));
    }

    [Theory]
    [InlineData("/stock-balance")]
    [InlineData("/stock-balance/site/record")]
    [InlineData("/stock-transfers")]
    [InlineData("/stock-transfers/site/record")]
    [InlineData("/daily-material-usage")]
    public void Site_selectors_are_hidden_when_the_required_site_lookup_is_forbidden(string route)
    {
        Assert.False(new WorkspaceAccess(["Supervisor"], ["Procurement_Inventory_View"]).CanVisit(route));
        Assert.True(new WorkspaceAccess(["Supervisor"], ["Procurement_Inventory_View", "Sites_View"]).CanVisit(route));
    }

    [Fact]
    public void View_only_supervisor_cannot_approve_even_with_approval_permissions()
    {
        var access = new WorkspaceAccess(["Supervisor"], ["HRM_Payroll_Approve", "HRM_Leave_Approve", "HRM_Attendance_Approve", "HRM_Shift_Submit", "HRM_SiteAssignment_Manage"]);
        Assert.False(access.CanApprovePayroll);
        Assert.False(access.CanReviewLeave);
        Assert.False(access.CanApproveAttendance);
        Assert.False(access.CanManageShifts);
        Assert.False(access.CanManageSiteAssignments);
        Assert.False(access.CanReviewTimesheets);
    }

    [Fact]
    public void Executive_endpoint_requires_both_report_permissions()
    {
        var policies = typeof(DashboardController).GetMethod(nameof(DashboardController.GetExecutiveDashboard))!
            .GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToArray();
        Assert.Contains("Reports.View", policies);
        Assert.Contains("Reports.FinancialSummary.View", policies);
        Assert.False(new WorkspaceAccess(["Supervisor"], ["Reports_View"]).CanViewExecutive);
        Assert.True(new WorkspaceAccess(["CompanyAdmin"], ["Reports_View", "Reports_FinancialSummary_View"]).CanViewExecutive);
    }

    [Theory]
    [InlineData("/hr/employees", "HRM_Employee_View", false)]
    [InlineData("/hr/payroll/create", "HRM_Payroll_View", false)]
    [InlineData("/hr/payroll/edit/record", "HRM_Payroll_View", true)]
    public void Direct_route_gate_stops_denied_page_lifecycle(string route, string permission, bool allowed)
    {
        Probe.Starts = 0;
        using var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, WorkspaceVisibilityTests.Auth>();
        ((WorkspaceVisibilityTests.Auth)(object)auth).Role = "Supervisor";
        ((WorkspaceVisibilityTests.Auth)(object)auth).Permissions = [permission];
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(DispatchProxy.Create<INotificationApiService, NoNotifications>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        var page = context.RenderComponent<WorkspaceRouteView>(p => p.Add(x => x.RouteData, new RouteData(typeof(Probe), new Dictionary<string, object>())));
        if (allowed) page.WaitForAssertion(() => Assert.Contains("Protected record", page.Markup));
        else page.WaitForAssertion(() => Assert.Contains("You do not have permission", page.Markup));
        Assert.Equal(allowed ? 1 : 0, Probe.Starts);
        Assert.Equal(allowed, page.Markup.Contains("Protected record"));
    }

    public sealed class Probe : ComponentBase
    {
        public static int Starts;
        protected override void OnInitialized() => Starts++;
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "Protected record");
    }
    public class NoNotifications : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(0);
    }
}
