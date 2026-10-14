using CSM.Domain.Common;

namespace CSM.Domain.Entities.Identity;

public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();

    public ICollection<RolePermission> Permissions { get; set; }
        = new List<RolePermission>();

    public ICollection<RoleGranularPermission> GranularPermissions { get; set; }
        = new List<RoleGranularPermission>();
}