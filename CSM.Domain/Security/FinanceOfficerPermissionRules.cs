namespace CSM.Domain.Security;

/// <summary>FinanceOfficer manages site-owned finance, with read-only shared finance lookups.</summary>
public static class FinanceOfficerPermissionRules
{
    public static bool Allows(string? roleName, string permission) =>
        !string.Equals(roleName, "FinanceOfficer", StringComparison.OrdinalIgnoreCase) || permission is
            "Sites_View" or "Finance_CostCode_View" or
            "Finance_Budget_View" or "Finance_Budget_Manage" or
            "Finance_Expense_View" or "Finance_Expense_Submit" or "Finance_Expense_Approve" or
            "Finance_Invoice_View" or "Finance_Payment_View" or "Finance_Payment_Manage" or
            "Finance_PettyCash_View" or "Finance_PettyCash_Manage" or
            "Reports_View" or "Reports_FinancialSummary_View";
}
