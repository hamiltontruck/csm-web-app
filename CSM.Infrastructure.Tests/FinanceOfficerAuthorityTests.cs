using System.Reflection;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Users.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class FinanceOfficerAuthorityTests
{
    internal static readonly GranularPermission[] Defaults = [
        GranularPermission.Sites_View, GranularPermission.Finance_CostCode_View,
        GranularPermission.Finance_Budget_View, GranularPermission.Finance_Budget_Manage,
        GranularPermission.Finance_Expense_View, GranularPermission.Finance_Expense_Submit, GranularPermission.Finance_Expense_Approve,
        GranularPermission.Finance_Invoice_View, GranularPermission.Finance_Payment_View, GranularPermission.Finance_Payment_Manage,
        GranularPermission.Finance_PettyCash_View, GranularPermission.Finance_PettyCash_Manage,
        GranularPermission.Reports_View, GranularPermission.Reports_FinancialSummary_View];

    [Fact]
    public async Task Startup_seeds_a_canonical_finance_role_with_only_its_supported_defaults_idempotently()
    {
        using var db = Database(); await Seed(db, "SeedRolesAsync"); await Seed(db, "SeedGranularRolePermissionsAsync");
        var role = Assert.Single(await db.Roles.Where(x => x.Name == "FinanceOfficer").ToListAsync());
        Assert.Equal("FINANCEOFFICER", role.NormalizedName);
        var actual = await db.RoleGranularPermissions.Where(x => x.RoleId == role.Id).Select(x => x.Permission).ToListAsync();
        Assert.Equal(Defaults.OrderBy(x => x), actual.OrderBy(x => x));
        var accountant = await db.Roles.SingleAsync(x => x.Name == "Accountant");
        var before = await db.RoleGranularPermissions.Where(x => x.RoleId == accountant.Id).Select(x => x.Permission).ToListAsync();
        await Seed(db, "SeedRolesAsync"); await Seed(db, "SeedGranularRolePermissionsAsync");
        Assert.Equal(1, await db.Roles.CountAsync(x => x.Name == "FinanceOfficer"));
        Assert.Equal(actual.Count, await db.RoleGranularPermissions.CountAsync(x => x.RoleId == role.Id));
        Assert.Equal(before.OrderBy(x => x), (await db.RoleGranularPermissions.Where(x => x.RoleId == accountant.Id).Select(x => x.Permission).ToListAsync()).OrderBy(x => x));
    }

    [Fact]
    public async Task Restart_preserves_an_administrators_revoked_supported_finance_grant()
    {
        using var db = Database(); await Seed(db, "SeedRolesAsync"); await Seed(db, "SeedGranularRolePermissionsAsync");
        var grant = await db.RoleGranularPermissions.SingleAsync(x => x.Role.Name == "FinanceOfficer" && x.Permission == GranularPermission.Finance_Budget_Manage);
        db.RoleGranularPermissions.Remove(grant); await db.SaveChangesAsync();
        await Seed(db, "SeedRolesAsync"); await Seed(db, "SeedGranularRolePermissionsAsync");
        Assert.False(await db.RoleGranularPermissions.AnyAsync(x => x.Role.Name == "FinanceOfficer" && x.Permission == GranularPermission.Finance_Budget_Manage));
    }

    [Fact]
    public async Task Seeded_finance_role_can_be_assigned_by_user_creation_with_its_selected_site()
    {
        using var db = Database(); await Seed(db, "SeedRolesAsync"); await Seed(db, "SeedGranularRolePermissionsAsync");
        var company = new Company { Code = "OWN", Name = "Own" };
        var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "SITE", Name = "Assigned", IsActive = true };
        var admin = new User { Email = "admin@example.test", NormalizedEmail = "ADMIN@EXAMPLE.TEST", UserRoles = [new() { Role = await db.Roles.SingleAsync(x => x.Name == "SuperAdmin") }] };
        db.Companies.Add(company); db.ConstructionSites.Add(site); db.Users.Add(admin); await db.SaveChangesAsync();
        var created = await new UserService(db, new TestHasher()).CreateAsync(admin.Id, new CreateUserRequest(company.Id, null,
            "finance@example.test", "Test", "Finance Officer", "Test-only-Password-2026!", "123", "Finance Officer", site.Id, ["FinanceOfficer"], [site.Id]));
        Assert.Contains("FinanceOfficer", created.Roles);
        Assert.True(await db.UserSiteAccesses.AnyAsync(x => x.UserId == created.Id && x.SiteId == site.Id && x.IsActive));
    }

    [Theory]
    [InlineData("Users_View")] [InlineData("HRM_Payroll_View")] [InlineData("Procurement_PurchaseOrder_Approve")]
    [InlineData("Finance_CostCode_Manage")] [InlineData("Reports_Export")] [InlineData("Notifications_View")]
    public void Stale_finance_role_grants_do_not_create_unrelated_or_company_shared_write_authority(string permission) =>
        Assert.False(RolePermissionRules.Allows("FinanceOfficer", permission));

    [Theory]
    [InlineData("FinanceOfficer")] [InlineData("financeofficer")]
    public async Task Database_permission_evaluator_rejects_stale_outside_ceiling_grants(string roleName)
    {
        using var db = Database(); var actor = Actor();
        actor.UserRoles.Single().Role.Name = roleName;
        actor.UserRoles.Single().Role.GranularPermissions.Add(new RoleGranularPermission { Permission = GranularPermission.Users_View });
        db.Users.Add(actor); await db.SaveChangesAsync();
        Assert.False(await new CSM.Infrastructure.Security.PermissionEvaluator(db).HasPermissionAsync(actor.Id, "Users_View"));
    }

    [Fact]
    public void Finance_role_ceiling_is_the_explicit_site_finance_permission_set() =>
        Assert.Equal(Defaults.OrderBy(x => x), Enum.GetValues<GranularPermission>().Where(x => RolePermissionRules.Allows("FinanceOfficer", x.ToString())).OrderBy(x => x));

    [Theory]
    [InlineData("Finance_Budget_Manage", "FinanceOfficer")]
    [InlineData("Finance_Budget_View", "Accountant")]
    public async Task Mixed_roles_are_resolved_per_action_without_mutating_tracked_role_links(string action, string expected)
    {
        using var db = Database(); var actor = Actor();
        actor.UserRoles.Add(new UserRole { Role = Role("Accountant", GranularPermission.Finance_Budget_View) });
        db.Users.Add(actor); await db.SaveChangesAsync();
        var effective = await Resolve(db, actor.Id, Enum.Parse<GranularPermission>(action));
        Assert.Equal(expected, Assert.Single(effective!.UserRoles).Role.Name);
        Assert.Equal(2, actor.UserRoles.Count);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("deleted-user")] [InlineData("deleted-link")] [InlineData("deleted-role")]
    [InlineData("missing-grant")] [InlineData("missing-company")]
    public async Task Unavailable_actor_or_live_grant_cannot_resolve_finance_authority(string state)
    {
        using var db = Database(); var actor = Actor(); var link = actor.UserRoles.Single();
        if (state == "inactive") actor.IsActive = false;
        if (state == "deleted-user") actor.IsDeleted = true;
        if (state == "deleted-link") link.IsDeleted = true;
        if (state == "deleted-role") link.Role.IsDeleted = true;
        if (state == "missing-grant") link.Role.GranularPermissions.Clear();
        if (state == "missing-company") actor.CompanyId = null;
        db.Users.Add(actor); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UserManagementException>(() => Resolve(db, actor.Id, GranularPermission.Finance_Budget_Manage));
    }

    [Theory]
    [InlineData("active", true)] [InlineData("missing", false)] [InlineData("inactive", false)]
    [InlineData("deleted", false)] [InlineData("foreign-link", false)] [InlineData("foreign-site", false)]
    [InlineData("inactive-site", false)] [InlineData("deleted-site", false)]
    public async Task Finance_site_scope_requires_a_live_valid_assignment(string state, bool allowed)
    {
        using var db = Database(); var actor = Actor(); var site = new ConstructionSite { CompanyId = actor.CompanyId!.Value, Name = "Assigned", SiteCode = "SITE", IsActive = true };
        var link = new UserSiteAccess { UserId = actor.Id, CompanyId = actor.CompanyId.Value, SiteId = site.Id, IsActive = true };
        if (state == "inactive") link.IsActive = false;
        if (state == "deleted") link.IsDeleted = true;
        if (state == "foreign-link") link.CompanyId = Guid.NewGuid();
        if (state == "foreign-site") site.CompanyId = Guid.NewGuid();
        if (state == "inactive-site") site.IsActive = false;
        if (state == "deleted-site") site.IsDeleted = true;
        db.Users.Add(actor); db.ConstructionSites.Add(site); if (state != "missing") db.UserSiteAccesses.Add(link); await db.SaveChangesAsync();
        Assert.True(SiteFinanceScope.IsSiteLimited(actor));
        Assert.Equal(allowed, await SiteFinanceScope.HasSiteAsync(db, actor, site.Id, default));
        Assert.False(await SiteFinanceScope.HasSiteAsync(db, actor, null, default));
    }

    private static Task<User?> Resolve(ApplicationDbContext db, Guid actorId, GranularPermission permission) =>
        CSM.Infrastructure.Security.FinanceOfficerScope.ResolveActorAsync(db, actorId, permission, CancellationToken.None,
            message => new UserManagementException(message));
    internal static User Actor() => new() { CompanyId = Guid.NewGuid(), Email = "finance@example.test", NormalizedEmail = "FINANCE@EXAMPLE.TEST", UserRoles = [new() { Role = Role("FinanceOfficer", Defaults) }] };
    internal static Role Role(string name, params GranularPermission[] grants) => new() { Name = name, NormalizedName = name.ToUpperInvariant(), GranularPermissions = grants.Select(p => new RoleGranularPermission { Permission = p }).ToList() };
    private static async Task Seed(ApplicationDbContext db, string method) => await (Task)typeof(DatabaseInitializer).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [db, CancellationToken.None])!;
    internal static TestDb Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    internal sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    { public override Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(true, ct); }
    private sealed class TestHasher : IPasswordHasher { public string Hash(string value) => "test-only-hash"; public bool Verify(string value, string hash) => false; }
}
