namespace CSM.Domain.Security;

/// <summary>Accountant can manage Finance and Purchase Orders, with no Users access.</summary>
public static class AccountantPermissionRules
{
    public static bool Allows(string? roleName, string permission) =>
        !string.Equals(roleName, "Accountant", StringComparison.Ordinal) ||
        (!permission.StartsWith("Users_", StringComparison.Ordinal) &&
         (permission.StartsWith("Finance_", StringComparison.Ordinal) ||
          permission.EndsWith("_View", StringComparison.Ordinal) ||
          permission is "Procurement_PurchaseOrder_Manage" or "Procurement_PurchaseOrder_Approve" or "Reports_Export"));
}
