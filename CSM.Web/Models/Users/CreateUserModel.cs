namespace CSM.Web.Models.Users;

public class CreateUserModel
{
    public Guid? CompanyId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string Email { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string TemporaryPassword { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string JobTitle { get; set; } = "";

    public Guid? DefaultSiteId { get; set; }

    public List<string> Roles { get; set; } = [];

    public List<Guid> SiteIds { get; set; } = [];
}