using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Identity;

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public Permission Permission { get; set; }
}