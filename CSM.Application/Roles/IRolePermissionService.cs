using CSM.Application.Roles.Dtos;

namespace CSM.Application.Roles;

public interface IRolePermissionService
{
    Task<IReadOnlyCollection<RoleSummaryDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RolePermissionDto>> GetPermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task UpdatePermissionsAsync(
        Guid actorId,
        Guid roleId,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default);
}