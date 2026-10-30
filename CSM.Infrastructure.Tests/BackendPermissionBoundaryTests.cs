using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class BackendPermissionBoundaryTests
{
    [Theory]
    [InlineData("inactive-user")]
    [InlineData("deleted-user")]
    [InlineData("deleted-membership")]
    [InlineData("deleted-role")]
    public async Task Revoked_identity_cannot_use_or_list_role_permissions(string revoked)
    {
        using var db = Database("CompanyAdmin", out var actor);
        var role = actor.UserRoles.Single();
        if (revoked == "inactive-user") actor.IsActive = false;
        if (revoked == "deleted-user") actor.IsDeleted = true;
        if (revoked == "deleted-membership") role.IsDeleted = true;
        if (revoked == "deleted-role") role.Role.IsDeleted = true;
        db.SaveChanges(true);
        var evaluator = new PermissionEvaluator(db);
        Assert.False(await evaluator.HasPermissionAsync(actor.Id, "Users_View"));
        Assert.Empty(await evaluator.GetPermissionsAsync(actor.Id));
    }

    [Fact]
    public async Task Active_actor_keeps_only_live_grants()
    {
        using var db = Database("CompanyAdmin", out var actor);
        var evaluator = new PermissionEvaluator(db);
        Assert.True(await evaluator.HasPermissionAsync(actor.Id, "Users_View"));
        Assert.False(await evaluator.HasPermissionAsync(actor.Id, "Users_Delete"));
        Assert.False(await evaluator.HasPermissionAsync(actor.Id, "not-a-permission"));
        Assert.Contains("Users_View", await evaluator.GetPermissionsAsync(actor.Id));
    }

    [Theory]
    [InlineData("CompanyAdmin", true)]
    [InlineData("Supervisor", true)]
    [InlineData("SuperAdmin", false)]
    public async Task Global_role_updates_require_superadmin_and_write_permission(string role, bool edit)
    {
        using var db = Database(role, out var actor);
        if (!edit)
        {
            db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Users_Edit));
            db.SaveChanges(true);
        }
        using var services = Services(db);
        var allowed = await Authorize(services, actor, nameof(RolesController.UpdatePermissions));
        Assert.False(allowed);
    }

    [Fact]
    public async Task Company_admin_keeps_role_listing_and_superadmin_with_write_grant_can_update()
    {
        using var db = Database("CompanyAdmin", out var actor);
        using var services = Services(db);
        Assert.True(await Authorize(services, actor, nameof(RolesController.GetRoles)));
        actor.UserRoles.Single().Role.Name = "SuperAdmin";
        db.SaveChanges(true);
        Assert.True(await Authorize(services, actor, nameof(RolesController.UpdatePermissions)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Previously_issued_token_is_rejected_for_disabled_or_deleted_user(bool active, bool deleted)
    {
        using var db = Database("CompanyAdmin", out var actor);
        actor.IsActive = active; actor.IsDeleted = deleted; db.SaveChanges(true);
        using var services = JwtServices(db);
        var context = JwtContext(services, actor.Id);
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>().Get("Bearer");
        await options.Events.TokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }

    [Fact]
    public async Task Previously_issued_token_cannot_keep_old_roles_or_company_after_reassignment()
    {
        using var db = Database("Supervisor", out var actor);
        actor.CompanyId = Guid.NewGuid(); db.SaveChanges(true);
        using var services = JwtServices(db);
        var context = JwtContext(services, actor.Id);
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>().Get("Bearer");
        await options.Events.TokenValidated(context);
        Assert.Null(context.Result?.Failure);
        Assert.False(context.Principal!.IsInRole("SuperAdmin"));
        Assert.True(context.Principal.IsInRole("Supervisor"));
        Assert.Equal(actor.CompanyId.ToString(), context.Principal.FindFirst("company_id")?.Value);
    }

    private static ServiceProvider JwtServices(ApplicationDbContext db)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:SecretKey"] = "test-secret-with-at-least-thirty-two-characters", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test"
        }).Build();
        var services = new ServiceCollection().AddLogging();
        CSM.Infrastructure.DependencyInjection.AddInfrastructure(services, config);
        services.AddSingleton(db);
        return services.BuildServiceProvider();
    }

    private static Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext JwtContext(IServiceProvider services, Guid actorId)
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = services };
        return new(http, new Microsoft.AspNetCore.Authentication.AuthenticationScheme("Bearer", null, typeof(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler)), new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions()) {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim(ClaimTypes.Role, "SuperAdmin"), new Claim("company_id", Guid.NewGuid().ToString())], "Bearer"))
        };
    }

    [Theory]
    [InlineData("CompanyAdmin")]
    [InlineData("Supervisor")]
    public async Task Service_rejects_global_grant_changes_without_modifying_existing_permissions(string role)
    {
        using var db = Database(role, out var actor);
        var target = new Role { Name = "Target", GranularPermissions = [new() { Permission = GranularPermission.Projects_View }] };
        db.Roles.Add(target); db.SaveChanges(true);
        var service = new CSM.Infrastructure.Services.RolePermissionService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdatePermissionsAsync(actor.Id, target.Id, new([GranularPermission.Users_Delete])));
        Assert.Equal(GranularPermission.Projects_View, db.RoleGranularPermissions.Single(x => x.RoleId == target.Id).Permission);
    }

    [Fact]
    public async Task Superadmin_replaces_only_target_role_grants_and_rejects_invalid_values_before_writing()
    {
        using var db = Database("SuperAdmin", out var actor);
        var target = new Role { Name = "Target", GranularPermissions = [new() { Permission = GranularPermission.Projects_View }] };
        db.Roles.Add(target); db.SaveChanges(true);
        var service = new CSM.Infrastructure.Services.RolePermissionService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePermissionsAsync(actor.Id, target.Id, new([(GranularPermission)9999])));
        Assert.Equal(GranularPermission.Projects_View, db.RoleGranularPermissions.Single(x => x.RoleId == target.Id).Permission);
        await service.UpdatePermissionsAsync(actor.Id, target.Id, new([GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_View]));
        Assert.Equal(GranularPermission.Procurement_Inventory_View, db.RoleGranularPermissions.Single(x => x.RoleId == target.Id).Permission);
        Assert.True(await new PermissionEvaluator(db).HasPermissionAsync(actor.Id, "Users_Edit"));
    }

    private static ServiceProvider Services(ApplicationDbContext db)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options => {
            options.AddPolicy("Users.View", p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement("Users_View")));
            options.AddPolicy("Users.Edit", p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement("Users_Edit")));
        });
        services.AddSingleton<IPermissionEvaluator>(new PermissionEvaluator(db));
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services.BuildServiceProvider();
    }

    private static async Task<bool> Authorize(IServiceProvider services, User actor, string action)
    {
        var data = typeof(RolesController).GetCustomAttributes<AuthorizeAttribute>().Concat(typeof(RolesController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, actor.UserRoles.Single().Role.Name)], "test"));
        return (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy!)).Succeeded;
    }

    private static ApplicationDbContext Database(string role, out User actor)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        actor = new User { IsActive = true, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = [new() { Permission = GranularPermission.Users_View }, new() { Permission = GranularPermission.Users_Edit }] } }] };
        db.Users.Add(actor); db.SaveChanges(true); return db;
    }
}
