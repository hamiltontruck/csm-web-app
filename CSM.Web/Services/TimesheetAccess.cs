namespace CSM.Web.Services;

// UI capabilities mirror the existing API service roles; the API remains authoritative.
public static class TimesheetAccess
{
    public static bool CanManage(IEnumerable<string> roles) =>
        roles.Any(role => role is "SuperAdmin" or "CompanyAdmin" or "SiteManager" or "HrOfficer" or "HROfficer");

    public static bool CanApprove(IEnumerable<string> roles) =>
        roles.Any(role => role is "SuperAdmin" or "CompanyAdmin" or "SiteManager");
}
