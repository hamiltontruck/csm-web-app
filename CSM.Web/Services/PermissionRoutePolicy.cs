namespace CSM.Web.Services;

public static class PermissionRoutePolicy
{
    private static readonly string[] FinanceViewPermissions =
    [
        "Finance_Budget_View",
        "Finance_CostCode_View",
        "Finance_Expense_View",
        "Finance_Invoice_View",
        "Finance_Payment_View",
        "Finance_PettyCash_View"
    ];

    private static readonly string[] HrViewPermissions =
    [
        "HRM_Employee_View",
        "HRM_Contractor_View",
        "HRM_Shift_View",
        "HRM_Payroll_View",
        "HRM_SiteAssignment_View",
        "HRM_Attendance_View",
        "HRM_Leave_View",
        "HRM_Timesheet_View"
    ];

    private static readonly string[] ProcurementViewPermissions =
    [
        "Procurement_MaterialRequest_View",
        "Procurement_PurchaseOrder_View",
        "Procurement_Inventory_View",
        "Procurement_Vendor_View"
    ];

    public static IReadOnlyCollection<string> GetRequiredPermissions(
        string relativePath)
    {
        var path = relativePath
            .Split('?', '#')[0]
            .Trim('/')
            .ToLowerInvariant();

        if (path.Length == 0 ||
            path is "login" or "logout" or "settings" or
                "change-password-required" or "counter" or "not-found")
        {
            return [];
        }

        if (path is "users/audit" or "audit" ||
            path.StartsWith("audit/", StringComparison.Ordinal))
        {
            return ["Audit_View"];
        }

        if (path.StartsWith("users/create", StringComparison.Ordinal))
        {
            return ["Users_Create"];
        }

        if (path.StartsWith("users/edit", StringComparison.Ordinal))
        {
            return ["Users_Edit"];
        }

        if (path is "users" or "roles" ||
            path.StartsWith("users/", StringComparison.Ordinal))
        {
            return ["Users_View"];
        }

        if (path == "finance" ||
            path.StartsWith("finance/", StringComparison.Ordinal))
        {
            if (path.Contains("/payments", StringComparison.Ordinal) &&
                IsMutationRoute(path))
            {
                return ["Finance_Payment_Manage"];
            }

            if (path.Contains("/payments", StringComparison.Ordinal))
            {
                return ["Finance_Payment_View"];
            }

            if (path.Contains("budget", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["Finance_Budget_Manage"]
                    : ["Finance_Budget_View"];
            }

            if (path.Contains("cost-code", StringComparison.Ordinal) ||
                path.Contains("costcode", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["Finance_CostCode_Manage"]
                    : ["Finance_CostCode_View"];
            }

            if (path.Contains("expense", StringComparison.Ordinal))
            {
                return path.Contains("/create", StringComparison.Ordinal)
                    ? ["Finance_Expense_Submit"]
                    : ["Finance_Expense_View", "Finance_Expense_Submit", "Finance_Expense_Approve"];
            }

            if (path.Contains("invoice", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["Finance_Invoice_Manage"]
                    : ["Finance_Invoice_View"];
            }

            if (path.Contains("petty-cash", StringComparison.Ordinal) ||
                path.Contains("pettycash", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["Finance_PettyCash_Manage"]
                    : ["Finance_PettyCash_View"];
            }

            return FinanceViewPermissions;
        }

        if (path == "hr" ||
            path.StartsWith("hr/", StringComparison.Ordinal))
        {
            if (path.Contains("attendance/override", StringComparison.Ordinal))
            {
                return ["HRM_Attendance_Override"];
            }

            if (path.Contains("attendance/check", StringComparison.Ordinal))
            {
                return ["HRM_Attendance_Submit"];
            }

            if (path.Contains("payroll", StringComparison.Ordinal))
            {
                return ["HRM_Payroll_View", "HRM_Payroll_Approve"];
            }

            if (path.Contains("employee", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_Employee_Manage"]
                    : ["HRM_Employee_View"];
            }

            if (path.Contains("contractor", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_Contractor_Manage"]
                    : ["HRM_Contractor_View"];
            }

            if (path.Contains("shift", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_Shift_Submit"]
                    : ["HRM_Shift_View"];
            }

            if (path.Contains("site-assignment", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_SiteAssignment_Manage"]
                    : ["HRM_SiteAssignment_View"];
            }

            if (path.Contains("attendance", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_Attendance_Submit", "HRM_Attendance_Override"]
                    : ["HRM_Attendance_View"];
            }

            if (path.Contains("leave", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_Leave_Submit", "HRM_Leave_Approve"]
                    : ["HRM_Leave_View"];
            }

            if (path.Contains("timesheet", StringComparison.Ordinal))
            {
                return IsMutationRoute(path)
                    ? ["HRM_Timesheet_Submit", "HRM_Timesheet_Approve"]
                    : ["HRM_Timesheet_View"];
            }

            return HrViewPermissions;
        }

        if (path.StartsWith("projects/", StringComparison.Ordinal))
        {
            if (path.Contains("/daily-progress/create", StringComparison.Ordinal))
            {
                return ["Projects_DailyProgress_Create"];
            }

            if (path.Contains("/daily-progress", StringComparison.Ordinal))
            {
                return ["Projects_DailyProgress_View"];
            }

            if (IsMutationRoute(path))
            {
                return ["Projects_Manage"];
            }

            return ["Projects_View"];
        }

        if (path == "projects")
        {
            return ["Projects_View"];
        }

        if (path == "project-tracking")
        {
            return ["Projects_View"];
        }

        if (path == "equipment" ||
            path.StartsWith("equipment/", StringComparison.Ordinal) ||
            path.StartsWith("equipment-", StringComparison.Ordinal))
        {
            if (IsMutationRoute(path))
            {
                return ["Equipment_Manage"];
            }

            return ["Equipment_View", "Procurement_Inventory_View"];
        }

        if (path is "inventory" or "daily-material-usage" ||
            path.StartsWith("inventory/", StringComparison.Ordinal) ||
            path.StartsWith("daily-material-usage/", StringComparison.Ordinal) ||
            path.StartsWith("stock-", StringComparison.Ordinal) ||
            path.StartsWith("material-return-audit", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Procurement_Inventory_Adjust"]
                : ["Procurement_Inventory_View"];
        }

        if (path == "procurement" ||
            path.StartsWith("procurement/", StringComparison.Ordinal))
        {
            return ProcurementViewPermissions;
        }

        if (path == "materials" ||
            path.StartsWith("materials/", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Procurement_Inventory_Adjust"]
                : ["Procurement_Inventory_View"];
        }

        if (path == "material-requests" ||
            path.StartsWith("material-requests/", StringComparison.Ordinal))
        {
            return path.Contains("/create", StringComparison.Ordinal)
                ? ["Procurement_MaterialRequest_Submit"]
                : ["Procurement_MaterialRequest_View", "Procurement_MaterialRequest_Submit", "Procurement_MaterialRequest_Approve"];
        }

        if (path == "purchase-orders" ||
            path.StartsWith("purchase-orders/", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Procurement_PurchaseOrder_Manage", "Procurement_PurchaseOrder_Approve"]
                : ["Procurement_PurchaseOrder_View"];
        }

        if (path == "goods-receipts" ||
            path.StartsWith("goods-receipts/", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Procurement_PurchaseOrder_Manage"]
                : ["Procurement_PurchaseOrder_View"];
        }

        if (path == "vendors" ||
            path.StartsWith("vendors/", StringComparison.Ordinal))
        {
            return ["Procurement_Vendor_View"];
        }

        if (path == "compliance" ||
            path.StartsWith("compliance/", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Compliance_Manage"]
                : ["Compliance_View"];
        }

        if (path == "reports" ||
            path.StartsWith("reports/", StringComparison.Ordinal))
        {
            return ["Reports_View", "Reports_FinancialSummary_View"];
        }

        if (path == "notifications" ||
            path.StartsWith("notifications/", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Notifications_Manage"]
                : ["Notifications_View"];
        }

        if (path == "sites-management" ||
            path == "sites" ||
            path.StartsWith("sites/", StringComparison.Ordinal))
        {
            return IsMutationRoute(path)
                ? ["Sites_Manage"]
                : ["Sites_View", "Sites_Manage"];
        }

        return [];
    }

    private static bool IsMutationRoute(string path)
    {
        return path.Contains("/create", StringComparison.Ordinal) ||
            path.Contains("/edit", StringComparison.Ordinal) ||
            path.Contains("/delete", StringComparison.Ordinal) ||
            path.Contains("/approve", StringComparison.Ordinal);
    }

}
