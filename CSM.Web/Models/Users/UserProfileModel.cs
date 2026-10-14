namespace CSM.Web.Models.Users;

public class UserProfileModel
{
    public Guid Id { get; set; }

    public string EmployeeNumber { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string FullName => $"{FirstName} {LastName}";

    public string Email { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string JobTitle { get; set; } = "";

    public bool IsActive { get; set; }

    public Guid? DefaultSiteId { get; set; }

    public string? DefaultSiteName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public List<string> Roles { get; set; } = [];

    public List<UserSiteProfileModel> Sites { get; set; } = [];
}

public class UserSiteProfileModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public bool IsDefault { get; set; }
}