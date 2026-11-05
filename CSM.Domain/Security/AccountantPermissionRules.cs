namespace CSM.Domain.Security;

/// <summary>Accountant is read-only outside Finance. Each additional role keeps its own grants.</summary>
public static class AccountantPermissionRules
{
    public static bool Allows(string? roleName, string permission) =>
        !string.Equals(roleName, "Accountant", StringComparison.Ordinal) ||
        permission.StartsWith("Finance_", StringComparison.Ordinal) ||
        permission.EndsWith("_View", StringComparison.Ordinal) ||
        permission == "Reports_Export";
}
