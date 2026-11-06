using System.Security.Claims;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

/// <summary>Stock grants on ProcurementOfficer do not grant Equipment authority.
/// An independent existing role must supply its own Equipment action grant.</summary>
public sealed record EquipmentPermissionRequirement(GranularPermission Permission, string[]? EligibleRoles) : IAuthorizationRequirement
{
    public static void AddPolicies(AuthorizationOptions options)
    {
        void Add(string name, GranularPermission permission, string[]? roles) =>
            options.AddPolicy(name, p => p.RequireAuthenticatedUser().AddRequirements(new EquipmentPermissionRequirement(permission, roles)));
        Add("Equipment.Inventory.View", GranularPermission.Procurement_Inventory_View, null);
        Add("Equipment.Inventory.Adjust", GranularPermission.Procurement_Inventory_Adjust, null);
        Add("Equipment.Corporate.View", GranularPermission.Procurement_Inventory_View, ["Administrator", "Storekeeper", "Accountant"]);
        Add("Equipment.Corporate.Adjust", GranularPermission.Procurement_Inventory_Adjust, ["Administrator", "Storekeeper"]);
    }
}

public sealed class EquipmentPermissionHandler(ApplicationDbContext db) : AuthorizationHandler<EquipmentPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EquipmentPermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        var grants = await db.UserRoles.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && !x.Role.IsDeleted && x.User.IsActive && !x.User.IsDeleted)
            .Where(x => x.Role.Name != "ProcurementOfficer")
            .SelectMany(x => x.Role.GranularPermissions, (membership, grant) => new { Role = membership.Role.Name, grant.Permission })
            .ToListAsync();
        if (grants.Any(x => x.Permission == requirement.Permission &&
            (requirement.EligibleRoles is null || requirement.EligibleRoles.Contains(x.Role)) &&
            RolePermissionRules.Allows(x.Role, x.Permission.ToString()))) context.Succeed(requirement);
    }
}
