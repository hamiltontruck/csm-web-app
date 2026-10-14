using CSM.Domain.Enums;

namespace CSM.Application.Roles.Dtos;

public sealed record RoleSummaryDto(
    Guid Id,
    string Name
);

public sealed record RolePermissionDto(
    GranularPermission Permission,
    bool Granted
);

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyCollection<GranularPermission> Permissions
);