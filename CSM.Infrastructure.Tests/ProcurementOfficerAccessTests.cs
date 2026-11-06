using System.Reflection;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using CSM.Web.Services.Access;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ProcurementOfficerAccessTests
{
    private static readonly string[] Allowed = ["Sites_View", "Procurement_Vendor_View", "Procurement_MaterialRequest_View", "Procurement_MaterialRequest_Submit", "Procurement_PurchaseOrder_View", "Procurement_PurchaseOrder_Manage", "Procurement_Inventory_View", "Procurement_Inventory_Adjust", "Notifications_View", "Reports_View"];

    [Fact]
    public async Task Effective_permissions_match_contract_even_with_stale_excess_grants()
    {
        using var f = new Fixture();
        var permissions = await new PermissionEvaluator(f.Db).GetPermissionsAsync(f.Actor.Id);
        Assert.Equal(Allowed.Order(), permissions.Order());
    }

    [Theory]
    [InlineData("/procurement")] [InlineData("/material-requests")] [InlineData("/material-requests/create")]
    [InlineData("/purchase-orders")] [InlineData("/purchase-orders/create")]
    [InlineData("/vendors")] [InlineData("/materials")] [InlineData("/goods-receipts/create")]
    [InlineData("/notifications")] [InlineData("/reports")] [InlineData("/inventory")]
    public void Granted_officer_menu_exposes_permitted_routes(string route) =>
        Assert.True(new WorkspaceAccess([AppRoles.ProcurementOfficer], Allowed).CanVisit(route));

    [Theory]
    [InlineData("Users_View")] [InlineData("Finance_Budget_View")] [InlineData("Reports_FinancialSummary_View")]
    [InlineData("Procurement_PurchaseOrder_Approve")] [InlineData("Procurement_MaterialRequest_Approve")]
    public void Browser_ignores_stale_unsupported_grants(string permission) =>
        Assert.False(new WorkspaceAccess([AppRoles.ProcurementOfficer], Enum.GetNames<GranularPermission>()).Has(permission));

    [Fact]
    public async Task No_assignment_defaults_to_all_own_company_sites()
    {
        using var f = new Fixture();
        var sites = await new SiteService(f.Db).GetAllAsync(f.Actor.Id);
        Assert.Equal(new[] { f.First.Id, f.Second.Id }.Order(), sites.Select(x => x.Id).Order());
        Assert.Equal(f.Second.Id, (await new SiteService(f.Db).GetByIdAsync(f.Actor.Id, f.Second.Id)).Id);
    }

    [Fact]
    public async Task Assignment_limits_stock_list_and_detail_and_live_deactivation_restores_company_default()
    {
        using var f = new Fixture();
        var access = f.Assign(f.First);
        var service = new StockBalanceService(f.Db);
        Assert.Equal(f.First.Id, Assert.Single(await service.GetAllAsync(f.Actor.Id)).ConstructionSiteId);
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetByIdAsync(f.Actor.Id, f.SecondStock.Id));
        access.IsActive = false;
        f.Db.SaveChanges(true);
        Assert.Equal(2, (await service.GetAllAsync(f.Actor.Id)).Count);
        Assert.Equal(f.Second.Id, (await service.GetByIdAsync(f.Actor.Id, f.SecondStock.Id)).ConstructionSiteId);
    }

    [Fact]
    public async Task Invalid_active_assignment_does_not_broaden_to_company()
    {
        using var f = new Fixture();
        f.Assign(f.Foreign);
        Assert.Empty(await new StockBalanceService(f.Db).GetAllAsync(f.Actor.Id));
        Assert.Empty(await new SiteService(f.Db).GetAllAsync(f.Actor.Id));
    }

    [Fact]
    public async Task Revoked_inventory_grant_cannot_fall_through_to_legacy_officer_role()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.Remove(f.Role.GranularPermissions.Single(x => x.Permission == GranularPermission.Procurement_Inventory_View));
        f.Db.SaveChanges(true);
        await Assert.ThrowsAnyAsync<Exception>(() => new StockBalanceService(f.Db).GetAllAsync(f.Actor.Id));
    }

    [Fact]
    public async Task Unrelated_role_does_not_supply_global_scope()
    {
        using var f = new Fixture();
        f.Assign(f.First);
        f.Db.UserRoles.Add(new() { UserId = f.Actor.Id, Role = new Role { Name = AppRoles.Administrator } });
        f.Db.SaveChanges(true);
        Assert.Equal(f.First.Id, Assert.Single(await new StockBalanceService(f.Db).GetAllAsync(f.Actor.Id)).ConstructionSiteId);
    }

    [Fact]
    public async Task Granted_officer_can_read_core_procurement_without_assignment()
    {
        using var f = new Fixture();
        Assert.Empty(await new MaterialRequestService(f.Db).GetAllAsync(f.Actor.Id));
        Assert.Empty(await new PurchaseOrderService(f.Db).GetAllAsync(f.Actor.Id, null, null, null));
        Assert.Empty(await new GoodsReceiptService(f.Db).GetAllAsync(f.Actor.Id));
        Assert.Empty(await new VendorService(f.Db).GetAllAsync(f.Actor.Id, null));
    }

    [Fact]
    public async Task Startup_seeds_exact_contract_and_cleans_unsupported_grants()
    {
        using var f = new Fixture();
        var seed = typeof(DatabaseInitializer).GetMethod("SeedGranularRolePermissionsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)seed.Invoke(null, new object[] { f.Db, CancellationToken.None })!;
        Assert.Equal(Allowed.Order(), f.Db.RoleGranularPermissions.Where(x => x.RoleId == f.Role.Id).Select(x => x.Permission.ToString()).Order());
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Role Role { get; } = new() { Name = AppRoles.ProcurementOfficer, GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() };
        public User Actor { get; }
        public ConstructionSite First { get; }
        public ConstructionSite Second { get; }
        public ConstructionSite Foreign { get; }
        public StockItem SecondStock { get; }
        public Fixture()
        {
            var company = Guid.NewGuid();
            Actor = new User { CompanyId = company, IsActive = true, UserRoles = [new() { Role = Role }] };
            First = new() { CompanyId = company, Name = "First", SiteCode = "FIRST", IsActive = true };
            Second = new() { CompanyId = company, Name = "Second", SiteCode = "SECOND", IsActive = true };
            Foreign = new() { CompanyId = Guid.NewGuid(), Name = "Foreign", SiteCode = "FOREIGN", IsActive = true };
            var material = new Material { CompanyId = company, MaterialCode = "M", Name = "Material", UnitOfMeasure = "Bag" };
            SecondStock = new() { CompanyId = company, ConstructionSite = Second, Material = material };
            Db.Users.Add(Actor);
            Db.ConstructionSites.AddRange(First, Second, Foreign);
            Db.StockItems.AddRange(new StockItem { CompanyId = company, ConstructionSite = First, Material = material }, SecondStock);
            Db.SaveChanges(true);
        }
        public UserSiteAccess Assign(ConstructionSite site)
        {
            var access = new UserSiteAccess { UserId = Actor.Id, SiteId = site.Id, CompanyId = site.CompanyId, IsActive = true };
            Db.UserSiteAccesses.Add(access);
            Db.SaveChanges(true);
            return access;
        }
        public void Dispose() => Db.Dispose();
    }
}
