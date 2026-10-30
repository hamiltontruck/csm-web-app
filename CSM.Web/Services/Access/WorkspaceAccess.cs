namespace CSM.Web.Services.Access;

/// <summary>
/// UI visibility follows the intersection of API permissions, controller roles and
/// service roles. It grants no authority: APIs still enforce company/site access.
/// Keep module dependencies here too, so links never lead to a forbidden lookup.
/// </summary>
public sealed class WorkspaceAccess(IEnumerable<string> roles, IEnumerable<string> permissions)
{
    private readonly HashSet<string> _roles = new(roles, StringComparer.Ordinal);
    private readonly HashSet<string> _permissions = new(permissions, StringComparer.Ordinal);
    public static WorkspaceAccess None { get; } = new([], []);
    private static readonly string[] Managers = ["SuperAdmin", "CompanyAdmin", "SiteManager"];
    private static readonly string[] HrManagers = ["SuperAdmin", "CompanyAdmin", "HrOfficer", "HROfficer"];
    private static readonly string[] HrReaders = ["SuperAdmin", "CompanyAdmin", "SiteManager", "Supervisor", "HrOfficer", "HROfficer"];
    private static readonly string[] StockManagers = ["SuperAdmin", "Administrator", "Storekeeper", "ProcurementOfficer"];
    private static readonly string[] FinanceManagers = ["SuperAdmin", "CompanyAdmin", "Accountant"];
    private static readonly string[] FinanceReaders = ["SuperAdmin", "CompanyAdmin", "Accountant", "SiteManager"];
    public bool Has(string permission) => _permissions.Contains(permission);
    public bool IsIn(params string[] roles) => roles.Any(_roles.Contains);
    public bool CanViewExecutive => Has("Reports_View") && Has("Reports_FinancialSummary_View");
    public bool CanApproveAttendance => IsIn(Managers) && Has("HRM_Attendance_Approve");
    public bool CanReviewLeave => IsIn(HrReaders.Where(r => r != "Supervisor").ToArray()) && Has("HRM_Leave_Approve");
    public bool CanManageShifts => IsIn(HrManagers) && Has("HRM_Shift_Submit");
    public bool CanManageSiteAssignments => IsIn(HrManagers) && Has("HRM_SiteAssignment_Manage");
    public bool CanManagePayroll => IsIn(HrManagers);
    public bool CanApprovePayroll => IsIn("SuperAdmin", "CompanyAdmin") && Has("HRM_Payroll_Approve");
    public bool CanManageTimesheets => IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "HrOfficer", "HROfficer");
    public bool CanReviewTimesheets => IsIn(Managers);

    public bool CanVisit(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var path = url.Split('?', '#')[0].TrimEnd('/').ToLowerInvariant();
        if (path.Length == 0) path = "/";
        // Public pages remain responsible for sign-in / required password changes.
        if (path is "/" or "/login" or "/error" or "/not-found" or "/change-password-required") return true;
        if (_roles.Count == 0) return false;
        if (path is "/dashboard" or "/settings") return true;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var write = parts.Contains("create") || parts.Contains("edit");
        var root = parts[0];
        if (root == "companies") return IsIn("SuperAdmin");
        if (root is "users" or "roles")
            return IsIn("SuperAdmin", "CompanyAdmin", "Administrator") &&
                Has(root == "users" && parts.Contains("create") ? "Users_Create" :
                    root == "users" && parts.Contains("edit") ? "Users_Edit" : "Users_View");
        if (root == "notifications") return Has("Notifications_View");
        if (root == "project-tracking") return CanVisit("/projects") || CanVisit("/sites-management");
        if (root is "sites" or "sites-management")
        {
            if (parts.Contains("projects")) return Has("Sites_View") && CanVisit("/projects");
            return Has(write ? "Sites_Manage" : "Sites_View") && (!write || IsIn(Managers));
        }
        if (root == "projects")
        {
            if (parts.Contains("daily-progress"))
                return Has("Projects_View") && Has(write ? "Projects_DailyProgress_Create" : "Projects_DailyProgress_View") &&
                    IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "Supervisor");
            return Has(write ? "Projects_Manage" : "Projects_View") && (!write || IsIn(Managers));
        }
        if (root == "hr")
        {
            if (parts.Length == 1) return HrLinks.Any(CanVisit);
            var area = parts[1];
            if (area == "employees") return IsIn(HrManagers) && Has(write ? "HRM_Employee_Manage" : "HRM_Employee_View");
            // The contractors controller still uses the legacy HRManagement policy.
            if (area == "contractors") return (!write || IsIn("SuperAdmin", "CompanyAdmin")) && IsIn("Administrator", "HrOfficer", "HROfficer") &&
                IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "Supervisor") &&
                Has(write ? "HRM_Contractor_Manage" : "HRM_Contractor_View");
            if (area == "shifts") return IsIn(HrReaders) && Has("HRM_Shift_View") && (!write || CanManageShifts);
            if (area == "site-assignments") return IsIn(HrReaders) && Has("HRM_SiteAssignment_View") && (!write || CanManageSiteAssignments);
            if (area == "attendance")
            {
                if (!IsIn(HrReaders) || !Has("HRM_Attendance_View")) return false;
                if (parts.Contains("override")) return CanApproveAttendance;
                if (parts.Contains("check-in")) return Has("HRM_Attendance_Submit") && CanVisit("/hr/employees");
                if (parts.Contains("check-out")) return Has("HRM_Attendance_Submit");
                return true;
            }
            if (area == "leave") return IsIn(HrReaders) && Has("HRM_Leave_View") && (!write || Has("HRM_Leave_Submit"));
            if (area == "timesheets") return IsIn(HrReaders) && (!parts.Contains("create") || CanManageTimesheets);
            // /edit is the existing read/details route, including the paid payslip.
            if (area == "payroll") return Has("HRM_Payroll_View") &&
                (parts.Contains("create") ? CanManagePayroll && CanVisit("/hr/employees") :
                    IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "Supervisor", "Accountant", "HROfficer"));
            return false;
        }
        if (root == "equipment")
        {
            if (parts.Length == 1) return EquipmentLinks.Any(CanVisit);
            // Site selection, equipment lists and create forms depend on the
            // equipment / assignment endpoints, all restricted to these roles.
            if (!IsIn(Managers) || !Has("Procurement_Inventory_View")) return false;
            if (parts.Any(p => p.StartsWith("fuel") || p.StartsWith("utilization")))
                return IsIn("Administrator", "ProcurementOfficer", "Storekeeper");
            return !write || Has("Procurement_Inventory_Adjust");
        }
        if (root == "compliance") return IsIn(Managers) && Has(write ? "Compliance_Manage" : "Compliance_View");
        if (root == "procurement") return ProcurementLinks.Any(CanVisit);
        if (root is "materials" or "vendors") return IsIn(Managers) && Has(write ? "Procurement_Inventory_Adjust" : "Procurement_Inventory_View");
        if (root == "material-requests") return IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "Accountant") &&
            Has(write ? "Procurement_MaterialRequest_Submit" : "Procurement_MaterialRequest_View") && (!write || IsIn(Managers));
        if (root == "purchase-orders") return IsIn(Managers) && Has(write ? "Procurement_PurchaseOrder_Manage" : "Procurement_PurchaseOrder_View");
        if (root == "goods-receipts") return IsIn(Managers) && Has(write ? "Procurement_Inventory_Adjust" : "Procurement_Inventory_View") && (!write || IsIn(Managers));
        if (root is "inventory" or "stock-items") return InventoryLinks.Any(CanVisit);
        if (root == "stock-balance") return (parts.Length > 1 && !parts.Contains("site") || Has("Sites_View")) && Has("Procurement_Inventory_View") && IsIn("SuperAdmin", "Administrator", "Supervisor", "Storekeeper", "ProcurementOfficer");
        if (root == "stock-transfers") return Has("Sites_View") && Has(write ? "Procurement_Inventory_Adjust" : "Procurement_Inventory_View") &&
            (!write || IsIn("Administrator", "Storekeeper", "ProcurementOfficer"));
        if (root is "stock-movements" or "stock-issues" or "stock-returns" or "stock-adjustments" or "material-return-audit")
            return IsIn(StockManagers) && Has(write ? "Procurement_Inventory_Adjust" : "Procurement_Inventory_View");
        if (root == "daily-material-usage") return (parts.Length > 1 || Has("Sites_View")) && IsIn("SuperAdmin", "CompanyAdmin", "SiteManager", "Supervisor") &&
            Has(write ? "Procurement_Inventory_Adjust" : "Procurement_Inventory_View") && (!write || CanVisit("/materials"));
        if (root == "finance")
        {
            if (parts.Contains("summary")) return Has("Reports_FinancialSummary_View");
            if (parts.Contains("cost-codes")) return IsIn(FinanceManagers) && Has(write ? "Finance_CostCode_Manage" : "Finance_CostCode_View");
            if (parts.Contains("budgets")) return Has(write ? "Finance_Budget_Manage" : "Finance_Budget_View");
            if (parts.Contains("payments")) return IsIn(write ? FinanceManagers : FinanceReaders) && Has(write ? "Finance_Payment_Manage" : "Finance_Payment_View");
            if (parts.Contains("invoices")) return IsIn(FinanceManagers) && Has("Finance_Invoice_View") &&
                (!parts.Contains("lines") || IsIn("Administrator", "FinanceOfficer"));
            if (parts.Contains("expenses")) return IsIn(write ? FinanceManagers : FinanceReaders) && Has(write ? "Finance_Expense_Submit" : "Finance_Expense_View");
            if (parts.Contains("petty-cash")) return Has(write ? "Finance_PettyCash_Manage" : "Finance_PettyCash_View");
            if (parts.Contains("vendors")) return CanVisit("/vendors");
            return FinanceLinks.Any(CanVisit);
        }
        if (root == "reports") return Has("Reports_View") || Has("Reports_FinancialSummary_View");
        // Unregistered screens have no visible entry point and no page lifecycle.
        return false;
    }

    public static readonly string[] HrLinks = ["/hr/employees", "/hr/contractors", "/hr/attendance", "/hr/site-assignments", "/hr/shifts", "/hr/leave", "/hr/timesheets", "/hr/payroll"];
    public static readonly string[] EquipmentLinks = ["/equipment/list", "/equipment/sites", "/equipment/downtime-sites", "/equipment/maintenance-sites", "/equipment/fuel-sites", "/equipment/hourmeter-sites", "/equipment/utilization-sites", "/equipment/timesheet-sites"];
    public static readonly string[] ProcurementLinks = ["/materials", "/vendors", "/material-requests", "/purchase-orders", "/goods-receipts"];
    public static readonly string[] InventoryLinks = ["/stock-balance", "/stock-movements", "/stock-issues", "/daily-material-usage", "/material-return-audit", "/stock-returns", "/stock-transfers", "/stock-adjustments"];
    public static readonly string[] FinanceLinks = ["/finance/company/cost-codes", "/finance/company/budgets", "/finance/company/invoices", "/finance/company/payments", "/finance/company/expenses", "/finance/petty-cash", "/finance/summary"];
}
