using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CSM.Infrastructure.Security;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionEvaluator _permissionEvaluator;

    public PermissionAuthorizationHandler(
        IPermissionEvaluator permissionEvaluator)
    {
        _permissionEvaluator = permissionEvaluator;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim =
            context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return;

        var hasPermission =
            await _permissionEvaluator.HasPermissionAsync(
                userId,
                requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}