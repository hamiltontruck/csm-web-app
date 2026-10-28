namespace CSM.Web.Services.HRM;

public static class AttendanceAccess
{
    public static bool CanReview(IEnumerable<string> roles) => roles.Any(role =>
        string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "CompanyAdmin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "SiteManager", StringComparison.OrdinalIgnoreCase));
}
