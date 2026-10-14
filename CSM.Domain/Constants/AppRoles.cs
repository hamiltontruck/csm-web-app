namespace CSM.Domain.Constants;

public static class AppRoles
{
    // New official roles
    public const string Administrator = "Administrator";
    public const string ProjectManager = "ProjectManager";
    public const string SiteEngineer = "SiteEngineer";
    public const string Storekeeper = "Storekeeper";
    public const string ProcurementOfficer = "ProcurementOfficer";
    public const string FinanceOfficer = "FinanceOfficer";
    public const string HrOfficer = "HrOfficer";
    public const string EquipmentManager = "EquipmentManager";
    public const string SafetyOfficer = "SafetyOfficer";


    // Legacy roles (keep for compatibility)
    public const string SuperAdmin = "SuperAdmin";
    public const string CompanyAdmin = "CompanyAdmin";
    public const string SiteManager = "SiteManager";
    public const string Supervisor = "Supervisor";
    public const string Accountant = "Accountant";
    public const string HROfficer = "HROfficer";
    public const string Employee = "Employee";



    public static readonly string[] All =
    {
        SuperAdmin,
        CompanyAdmin,
        SiteManager,
        Accountant,
        HROfficer,
        Supervisor,
        Employee
    };
}