using CSM.Domain.Common;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Organization;

namespace CSM.Domain.Entities.Identity;

public class User : AuditableEntity
{
    public Guid? CompanyId { get; set; }

    public Guid? EmployeeId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public Company? Company { get; set; }

    public Employee? Employee { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public Guid? DefaultSiteId { get; set; }

    public ICollection<UserSiteAccess> SiteAccesses { get; set; }
        = new List<UserSiteAccess>();

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();

    public ICollection<RefreshToken> RefreshTokens { get; set; }
        = new List<RefreshToken>();
}