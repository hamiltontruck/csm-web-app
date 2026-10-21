using CSM.Application.Common.Security;
using CSM.Application.Users.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InventoryRoleSeedTests
{
    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.ProcurementOfficer)]
    public async Task Startup_seeds_inventory_role_and_allows_company_user_creation_with_only_inventory_access(string roleName)
    {
        using var db = Database();
        using var provider = Provider(db);
        await DatabaseInitializer.InitializeAsync(provider, new ConfigurationBuilder().Build());
        var role = Assert.Single(db.Roles.Where(x => x.NormalizedName == roleName.ToUpperInvariant()));
        var request = new CreateUserRequest(CompanyId, null, "inventory@example.test", "Test", "Inventory",
            "Example-Password123!", "", roleName, SiteId, [roleName], [SiteId]);
        var result = await new UserService(db, new Hasher()).CreateAsync(AdminId, request);
        Assert.Contains(roleName, result.Roles);
        Assert.Equal(CompanyId, result.CompanyId);
        Assert.Contains(SiteId, result.SiteAccesses);
        var evaluator = new PermissionEvaluator(db);
        Assert.True(await evaluator.HasPermissionAsync(result.Id, GranularPermission.Sites_View.ToString()));
        Assert.True(await evaluator.HasPermissionAsync(result.Id, GranularPermission.Procurement_Inventory_View.ToString()));
        Assert.True(await evaluator.HasPermissionAsync(result.Id, GranularPermission.Procurement_Inventory_Adjust.ToString()));
        Assert.False(await evaluator.HasPermissionAsync(result.Id, GranularPermission.Users_Create.ToString()));
        Assert.False(await evaluator.HasPermissionAsync(result.Id, GranularPermission.Finance_Payment_Manage.ToString()));
        Assert.False(await evaluator.HasPermissionAsync(result.Id, GranularPermission.Reports_Export.ToString()));
        Assert.Equal(role.Id, db.UserRoles.Single(x => x.UserId == result.Id).RoleId);
    }

    [Fact]
    public async Task Existing_StoreKeeper_spelling_keeps_role_id_and_receives_inventory_permissions()
    {
        using var db = Database();
        var roleId = Guid.NewGuid();
        db.Roles.Add(new Role { Id = roleId, Name = "StoreKeeper", NormalizedName = "STOREKEEPER" });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        using var provider = Provider(db);
        await DatabaseInitializer.InitializeAsync(provider, new ConfigurationBuilder().Build());
        Assert.Equal(roleId, Assert.Single(db.Roles.Where(x => x.NormalizedName == "STOREKEEPER")).Id);
        Assert.Contains(db.RoleGranularPermissions, x => x.RoleId == roleId &&
            x.Permission == GranularPermission.Procurement_Inventory_Adjust);
    }

    [Fact]
    public async Task Repeated_startup_does_not_duplicate_roles_permissions_or_reset_existing_admin()
    {
        using var db = Database();
        using var provider = Provider(db);
        var configuration = new ConfigurationBuilder().Build();
        await DatabaseInitializer.InitializeAsync(provider, configuration);
        var roles = await db.Roles.CountAsync();
        var inventoryRoleIds = await db.Roles.Where(x => x.NormalizedName == "STOREKEEPER" ||
            x.NormalizedName == "PROCUREMENTOFFICER").Select(x => x.Id).ToListAsync();
        var grants = await db.RoleGranularPermissions.CountAsync(x => inventoryRoleIds.Contains(x.RoleId));
        await DatabaseInitializer.InitializeAsync(provider, configuration);
        Assert.Equal(roles, await db.Roles.CountAsync());
        Assert.Equal(grants, await db.RoleGranularPermissions.CountAsync(x => inventoryRoleIds.Contains(x.RoleId)));
        Assert.Equal(1, await db.Roles.CountAsync(x => x.NormalizedName == "STOREKEEPER"));
        Assert.Equal("existing-admin-hash", db.Users.Single().PasswordHash);
    }

    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();

    private static TestDatabase Database()
    {
        var db = new TestDatabase(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.Add(new Company { Id = CompanyId, Name = "Hamilton", Code = "HBG" });
        db.ConstructionSites.Add(new ConstructionSite { Id = SiteId, CompanyId = CompanyId, Name = "Hamilton Test Site" });
        db.Users.Add(new User { Id = AdminId, IsActive = true, PasswordHash = "existing-admin-hash",
            UserRoles = [new UserRole { Role = new Role { Name = AppRoles.SuperAdmin, NormalizedName = "SUPERADMIN" } }] });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private static ServiceProvider Provider(ApplicationDbContext db) => new ServiceCollection()
        .AddSingleton(db).AddSingleton<IPasswordHasher, Hasher>().BuildServiceProvider();

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "test-hash";
        public bool Verify(string password, string passwordHash) => false;
    }
}
