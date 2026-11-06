namespace CSM.Domain.Security;

/// <summary>Apply each role's supported grants before combining independently assigned roles.</summary>
public static class RolePermissionRules
{
    public static bool Allows(string? roleName, string permission) =>
        AccountantPermissionRules.Allows(roleName, permission) &&
        (!string.Equals(roleName, "ProcurementOfficer", StringComparison.Ordinal) || permission is
            "Sites_View" or "Procurement_Vendor_View" or
            "Procurement_MaterialRequest_View" or "Procurement_MaterialRequest_Submit" or
            "Procurement_PurchaseOrder_View" or "Procurement_PurchaseOrder_Manage" or
            "Procurement_Inventory_View" or "Procurement_Inventory_Adjust" or
            "Notifications_View" or "Reports_View");
}
