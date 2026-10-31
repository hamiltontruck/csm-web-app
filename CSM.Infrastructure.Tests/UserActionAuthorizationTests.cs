using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Users.Dtos;
using CSM.Domain.Entities.Organization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
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
        registrations.AddSingleton<ApplicationDbContext>(fixture.Db);
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
            new UserAudit { CompanyId = f.Actor.CompanyId, ActorUser = f.Actor, TargetUser = f.Target, Action = "local" },
            new UserAudit { ActorUser = outsider, TargetUser = f.Target, Action = "foreign actor" },
            new UserAudit { ActorUser = f.Actor, TargetUser = outsider, Action = "foreign target" });
        f.Db.SaveChanges(true);
        Assert.Equal("local", Assert.Single(await f.Service.GetAuditAsync(f.Actor.Id)).Action);
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("SetRoles")]
    [InlineData("SetSites")]
    [InlineData("Activate")]
    [InlineData("Deactivate")]
    [InlineData("Delete")]
    public async Task Direct_mutations_require_the_action_grant_before_validation_or_writes(string action)
    {
        using var f = new Fixture(AppRoles.CompanyAdmin, false);
        Task Invoke() => action switch {
            "Create" => f.Service.CreateAsync(f.Actor.Id, new(f.Actor.CompanyId, null, "new@example.test", "New", "User", "StrongPassword-2026!", "", "", null, [AppRoles.Supervisor], [])),
            "Update" => f.Service.UpdateAsync(f.Actor.Id, f.Target.Id, new("Changed", "User", null, "", "", null, false, [])),
            "SetRoles" => f.Service.SetRolesAsync(f.Actor.Id, f.Target.Id, new([AppRoles.Supervisor]), default),
            "SetSites" => f.Service.SetSitesAsync(f.Actor.Id, f.Target.Id, new(null, [])),
            "Activate" => f.Service.ActivateAsync(f.Actor.Id, f.Target.Id),
            "Deactivate" => f.Service.DeactivateAsync(f.Actor.Id, f.Target.Id),
            "Delete" => f.Service.DeleteAsync(f.Actor.Id, f.Target.Id),
            _ => throw new InvalidOperationException()
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(Invoke);
        Assert.True(f.Target.IsActive);
        Assert.False(f.Target.IsDeleted);
        Assert.Equal(2, f.Db.Users.Count());
        Assert.Empty(f.Db.UserAudits);
    }

    [Fact]
    public async Task Administrative_profile_and_detail_require_view_and_stay_inside_company()
    {
        using var f = new Fixture(AppRoles.CompanyAdmin, true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetByIdAsync(f.Actor.Id, f.Target.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetProfileAsync(f.Actor.Id, f.Target.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetAuditAsync(f.Actor.Id));
        f.Db.RoleGranularPermissions.Add(new() { RoleId = f.Role.Id, Permission = GranularPermission.Users_View });
        f.Db.SaveChanges(true);
        Assert.Equal(f.Target.Id, (await f.Service.GetProfileAsync(f.Actor.Id, f.Target.Id)).Id);
        Assert.Equal(f.Target.Id, (await f.Service.GetByIdAsync(f.Actor.Id, f.Target.Id)).Id);
        f.Target.CompanyId = Guid.NewGuid(); f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetProfileAsync(f.Actor.Id, f.Target.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetByIdAsync(f.Actor.Id, f.Target.Id));
    }

    [Fact]
    public async Task Deleted_role_or_membership_cannot_authorize_activation()
    {
        using var f = new Fixture(AppRoles.CompanyAdmin, true);
        f.Role.IsDeleted = true; f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, false, default));
        f.Role.IsDeleted = false;
        f.Actor.UserRoles.Single().IsDeleted = true;
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SetActiveStatusAsync(f.Actor.Id, f.Target.Id, false, default));
        Assert.True(f.Target.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Service_failures_map_to_safe_HTTP_results(bool forbidden)
    {
        var context = new ExceptionContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), []) {
            Exception = forbidden ? new UnauthorizedAccessException("private details") : new KeyNotFoundException("private details")
        };
        new UsersAccessExceptionFilter().OnException(context);
        Assert.True(context.ExceptionHandled);
        if (forbidden) Assert.IsType<ForbidResult>(context.Result);
        else Assert.DoesNotContain("private", Assert.IsType<NotFoundObjectResult>(context.Result).Value!.ToString());
    }

    [Fact]
    public async Task PostgreSQL_enforces_live_user_activation_grants_and_company_boundaries()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") {
            Database = "user_actions_" + Guid.NewGuid().ToString("N")
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "USERS", Name = "User scope" };
            var foreign = new Company { Code = "OTHER", Name = "Other scope" };
            var adminRole = new Role { Name = AppRoles.CompanyAdmin, NormalizedName = "COMPANYADMIN", GranularPermissions = [new() { Permission = GranularPermission.Users_Edit }] };
            var normalRole = new Role { Name = AppRoles.Supervisor, NormalizedName = "SUPERVISOR", GranularPermissions = [new() { Permission = GranularPermission.Users_Edit }] };
            var admin = new User { CompanyId = company.Id, Email = "admin@example.test", NormalizedEmail = "ADMIN@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = adminRole }] };
            var normal = new User { CompanyId = company.Id, Email = "normal@example.test", NormalizedEmail = "NORMAL@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = normalRole }] };
            var outside = new User { CompanyId = foreign.Id, Email = "outside@example.test", NormalizedEmail = "OUTSIDE@EXAMPLE.TEST", IsActive = true };
            db.Companies.AddRange(company, foreign); db.Users.AddRange(admin, normal, outside);
            await db.SaveChangesAsync();
            var service = new UserService(db, new PasswordHasher());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetActiveStatusAsync(normal.Id, admin.Id, false, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetActiveStatusAsync(admin.Id, outside.Id, false, default));
            await service.SetActiveStatusAsync(admin.Id, normal.Id, false, default);
            Assert.False(normal.IsActive);
            db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions.Where(x => x.RoleId == adminRole.Id));
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetActiveStatusAsync(admin.Id, normal.Id, true, default));
            Assert.False(normal.IsActive);
            Assert.True(admin.IsActive);
            Assert.True(outside.IsActive);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }

    private sealed class Fixture : IDisposable
    {
        public TestDb Db { get; }
        public Role Role { get; }
        public User Actor { get; }
        public User Target { get; }
        public UserService Service { get; }
        public Fixture(string role, bool granted)
        {
            Db = new TestDb();
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
