namespace CSM.Domain.Enums;

public enum GranularPermission
{
    Users_View,
    Users_Create,
    Users_Edit,
    Users_Delete,

    Sites_View,
    Sites_Manage,

    Projects_View,
    Projects_Manage,
    Projects_DailyProgress_View,
    Projects_DailyProgress_Create,

    HRM_Employee_View,
    HRM_Employee_Manage,

    HRM_Contractor_View,
    HRM_Contractor_Manage,

    HRM_Shift_View,
    HRM_Shift_Submit,

    HRM_Payroll_View,
    HRM_Payroll_Approve,

    HRM_SiteAssignment_View,
    HRM_SiteAssignment_Manage,

    HRM_Attendance_View,
    HRM_Attendance_Submit,

    HRM_Leave_View,
    HRM_Leave_Submit,
    HRM_Leave_Approve,

    Finance_CostCode_View,
    Finance_CostCode_Manage,

    Finance_Budget_View,
    Finance_Budget_Manage,

    Finance_Expense_View,
    Finance_Expense_Submit,
    Finance_Expense_Approve,

    Finance_Invoice_View,

    Finance_Payment_View,
    Finance_Payment_Manage,

    Finance_PettyCash_View,
    Finance_PettyCash_Manage,

    Procurement_MaterialRequest_View,
    Procurement_MaterialRequest_Submit,
    Procurement_MaterialRequest_Approve,

    Procurement_PurchaseOrder_View,
    Procurement_PurchaseOrder_Manage,
    Procurement_PurchaseOrder_Approve,

    Procurement_Inventory_View,
    Procurement_Inventory_Adjust,

    Compliance_View,
    Compliance_Manage,

    Notifications_View,
    Notifications_Manage,

    Reports_View,
    Reports_FinancialSummary_View,
    Reports_Export,

    // Append permissions to preserve existing persisted numeric values.
    HRM_Attendance_Approve

}