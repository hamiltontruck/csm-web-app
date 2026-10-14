using CSM.Domain.Constants;

namespace CSM.Application.Common.Security;

public static class AppPolicies
{
    public const string UserManagement = "UserManagement";
    public const string SiteManagement = "SiteManagement";
    public const string HRManagement = "HRManagement";
    public const string FinanceManagement = "FinanceManagement";
    public const string ProcurementManagement = "ProcurementManagement";
    public const string CompanyManagement = "CompanyManagement";
    public const string ProjectManagement = "ProjectManagement";
    public const string EquipmentManagement = "EquipmentManagement";
    public const string ComplianceManagement = "ComplianceManagement";
    public const string ReportManagement = "ReportManagement";

    public static readonly string[] ProjectManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.CompanyAdmin,
        AppRoles.SuperAdmin,
        AppRoles.ProjectManager,
        AppRoles.SiteEngineer
    };

    public static readonly string[] CompanyManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.CompanyAdmin,
        AppRoles.SuperAdmin
    };

    public static readonly string[] UserManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.CompanyAdmin,
        AppRoles.SuperAdmin
    };

    public static readonly string[] SiteManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.ProjectManager,
        AppRoles.SiteEngineer
    };

    public static readonly string[] HRManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.HrOfficer,
        AppRoles.HROfficer
    };

    public static readonly string[] FinanceManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.FinanceOfficer
    };

    public static readonly string[] ProcurementManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.ProcurementOfficer,
        AppRoles.Storekeeper
    };

    public static readonly string[] EquipmentManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.EquipmentManager
    };

    public static readonly string[] ComplianceManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.SafetyOfficer
    };

    public static readonly string[] ReportManagementRoles =
    {
        AppRoles.Administrator,
        AppRoles.ProjectManager,
        AppRoles.SiteEngineer,
        AppRoles.Storekeeper,
        AppRoles.ProcurementOfficer,
        AppRoles.FinanceOfficer,
        AppRoles.HrOfficer,
        AppRoles.EquipmentManager,
        AppRoles.SafetyOfficer
    };
}