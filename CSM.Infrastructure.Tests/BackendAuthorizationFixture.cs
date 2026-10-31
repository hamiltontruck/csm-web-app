using System.Security.Claims;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CSM.Infrastructure.Tests;

/// <summary>Existing role-ceiling tests use real permission policies with a granted actor.
/// Missing-grant behavior is independently exercised by BackendActionPermissionTests.</summary>
internal static class BackendAuthorizationFixture
{
    internal static readonly Guid ActorId = Guid.Parse("134ef26b-bc15-4ebf-bb98-ff8f0e9599f5");
    public static IServiceCollection AddGrantedBackendPermissions(this IServiceCollection services, string role = "Supervisor", IEnumerable<GranularPermission>? permissions = null)
    {
        var grants = (permissions ?? Enum.GetValues<GranularPermission>()).ToArray();
        services.AddSingleton<ApplicationDbContext>(_ => {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            db.Users.Add(new User { Id = ActorId, IsActive = true, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = grants.Select(p => new RoleGranularPermission { Permission = p }).ToList() } }] });
            db.SaveChanges(true);
            return db;
        });
        services.AddSingleton<IPermissionEvaluator, PermissionEvaluator>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options => {
            foreach (var permission in Enum.GetValues<GranularPermission>())
                options.AddPolicy(permission.ToString().Replace('_', '.'), p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission.ToString())));
        });
        return services;
    }
    public static ClaimsPrincipal WithGrantedIdentity(ClaimsPrincipal principal) => new(new ClaimsIdentity(
        principal.Claims.Where(c => c.Type != ClaimTypes.NameIdentifier).Append(new Claim(ClaimTypes.NameIdentifier, ActorId.ToString())), "test"));
}
