namespace CSM.Web.Models.Users;

public class UpdateUserModel
{
    public Guid? EmployeeId { get; set; }

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string JobTitle { get; set; } = "";

    public Guid? DefaultSiteId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Guid> SiteIds { get; set; } = [];
}