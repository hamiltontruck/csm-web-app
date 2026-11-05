using CSM.Domain.Enums;

namespace CSM.Application.Roles.Dtos;

public sealed record RoleSummaryDto(
    Guid Id,
    string Name
);

public sealed record RolePermissionDto(
    GranularPermission Permission,
    bool Granted,
    bool CanGrant = true
);

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyCollection<GranularPermission> Permissions
);
