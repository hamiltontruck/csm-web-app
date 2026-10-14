using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Identity;

public class RoleGranularPermission : BaseEntity
{
    public Guid RoleId { get; set; }

    public GranularPermission Permission { get; set; }

    public Role Role { get; set; } = null!;
}