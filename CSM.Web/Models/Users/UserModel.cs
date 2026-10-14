namespace CSM.Web.Models.Users;

public class UserModel
{
    public Guid Id { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string Email { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string FullName { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string JobTitle { get; set; } = "";

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }

    public Guid? DefaultSiteId { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public List<string> Roles { get; set; } = [];

    // Must match API UserResponse
    public List<Guid> SiteAccesses { get; set; } = new();

    public List<string> SiteNames { get; set; } = new();
}