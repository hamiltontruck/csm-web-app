using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class UserActionAuthorizationTests
{
    [Theory]
    [InlineData("GetAll", GranularPermission.Users_View)]
    [InlineData("GetById", GranularPermission.Users_View)]
    [InlineData("GetAudit", GranularPermission.Users_View)]
    [InlineData("Create", GranularPermission.Users_Create)]
    [InlineData("Update", GranularPermission.Users_Edit)]
    [InlineData("SetRoles", GranularPermission.Users_Edit)]
    [InlineData("SetSites", GranularPermission.Users_Edit)]
    [InlineData("Activate", GranularPermission.Users_Edit)]
    [InlineData("Deactivate", GranularPermission.Users_Edit)]
    [InlineData("Delete", GranularPermission.Users_Delete)]
    public async Task Api_actions_require_their_canonical_grant(string action, GranularPermission permission)
    {
        using var fixture = new Fixture(AppRoles.SuperAdmin, false);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:SecretKey"] = "test-secret-with-at-least-thirty-two-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test"
        }).Build();
        var registrations = new ServiceCollection().AddLogging();
        CSM.Infrastructure.DependencyInjection.AddInfrastructure(registrations, config);
        registrations.AddSingleton(fixture.Db);
        using var services = registrations.BuildServiceProvider();
        var metadata = typeof(UsersController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(typeof(UsersController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, fixture.Actor.Id.ToString()), new Claim(ClaimTypes.Role, AppRoles.SuperAdmin)], "test"));
        var authorization = services.GetRequiredService<IAuthorizationService>();
        Assert.False((await authorization.AuthorizeAsync(principal, null, policy!)).Succeeded);
        fixture.Db.RoleGranularPermissions.Add(new() { RoleId = fixture.Role.Id, Permission = permission });
        fixture.Db.SaveChanges(true);
        Assert.True((await authorization.AuthorizeAsync(principal, null, policy!)).Succeeded);
    }

    [Theory]
    [InlineData("Supervisor", true, true, false, false)]
    [InlineData("CompanyAdmin", true, true, false, true)]
    [InlineData("CompanyAdmin", true, true, true, false)]
    [InlineData("CompanyAdmin", false, true, false, false)]
    [InlineData("CompanyAdmin", true, false, false, false)]
    [InlineData("SuperAdmin", true, true, true, true)]
    public async Task Activation_requires_live_active_admin_grant_and_company_boundary(string role, bool active, bool granted, bool foreign, bool allowed)
    {
        using var f = new Fixture(role, granted);
        f.Actor.IsActive = active;
        if (foreign) f.Target.CompanyId = Guid.NewGuid();
        f.Db.SaveChanges(true);
        if (allowed) {
            await f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, false, default);
            Assert.False(f.Target.IsActive);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, false, default));
            Assert.True(f.Target.IsActive);
        }
    }

    [Fact]
    public async Task Deleted_actor_and_revoked_grant_are_rejected_even_when_previously_tracked()
    {
        using var f = new Fixture(AppRoles.CompanyAdmin, true);
        await f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, false, default);
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions);
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, true, default));
        f.Actor.IsDeleted = true;
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, true, default));
        Assert.False(f.Target.IsActive);
    }

    [Fact]
    public async Task Company_admin_cannot_deactivate_SuperAdmin_even_in_same_company()
    {
        using var f = new Fixture(AppRoles.CompanyAdmin, true);
        f.Target.UserRoles.Add(new() { Role = new Role { Name = AppRoles.SuperAdmin } });
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, false, default));
        Assert.True(f.Target.IsActive);
    }

    [Fact]
    public async Task Self_profile_remains_available_without_user_management_grants()
    {
        using var f = new Fixture(AppRoles.Supervisor, false);
        Assert.Equal(f.Actor.Id, (await f.Service.GetProfileAsync(f.Actor.Id, f.Actor.Id)).Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetProfileAsync(f.Actor.Id, f.Target.Id));
    }

    [Fact]
    public async Task Audit_is_scoped_to_both_actor_and_target_company()
    {
        using var f = new Fixture(AppRoles.CompanyAdmin, true);
        f.Db.RoleGranularPermissions.Add(new() { RoleId = f.Role.Id, Permission = GranularPermission.Users_View });
        var outsider = new User { CompanyId = Guid.NewGuid(), IsActive = true };
        f.Db.Users.Add(outsider);
        f.Db.UserAudits.AddRange(
            new UserAudit { ActorUser = f.Actor, TargetUser = f.Target, Action = "local" },
            new UserAudit { ActorUser = outsider, TargetUser = f.Target, Action = "foreign actor" },
            new UserAudit { ActorUser = f.Actor, TargetUser = outsider, Action = "foreign target" });
        f.Db.SaveChanges(true);
        Assert.Equal("local", Assert.Single(await f.Service.GetAuditAsync(f.Actor.Id)).Action);
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; }
        public Role Role { get; }
        public User Actor { get; }
        public User Target { get; }
        public UserService Service { get; }
        public Fixture(string role, bool granted)
        {
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Role = new Role { Name = role };
            Actor = new User { CompanyId = Guid.NewGuid(), IsActive = true, UserRoles = [new() { Role = Role }] };
            Target = new User { CompanyId = Actor.CompanyId, IsActive = true };
            Db.Users.AddRange(Actor, Target);
            if (granted) Db.RoleGranularPermissions.Add(new() { Role = Role, Permission = GranularPermission.Users_Edit });
            Db.SaveChanges(true);
            Service = new UserService(Db, new PasswordHasher());
        }
        public void Dispose() => Db.Dispose();
    }
}
