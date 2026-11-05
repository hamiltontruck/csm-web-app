using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Application.Common.Exceptions;
using CSM.Application.Roles.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using CSM.Web.Services.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AccountantReadOnlyAccessTests
{
    [Theory]
    [InlineData("Users_Create", false)]
    [InlineData("Users_Edit", false)]
    [InlineData("Users_Delete", false)]
    [InlineData("Sites_Manage", false)]
    [InlineData("Projects_Manage", false)]
    [InlineData("Procurement_Inventory_Adjust", false)]
    [InlineData("Procurement_PurchaseOrder_Manage", false)]
    [InlineData("Procurement_PurchaseOrder_Approve", false)]
    [InlineData("Procurement_MaterialRequest_Submit", false)]
    [InlineData("Procurement_MaterialRequest_Approve", false)]
    [InlineData("Compliance_Manage", false)]
    [InlineData("Notifications_Manage", false)]
    [InlineData("Users_View", true)]
    [InlineData("Procurement_Inventory_View", true)]
    [InlineData("Procurement_PurchaseOrder_View", true)]
    [InlineData("Finance_Budget_Manage", true)]
    [InlineData("Finance_Expense_Approve", true)]
    [InlineData("Finance_Payment_Manage", true)]
    [InlineData("Finance_PettyCash_Manage", true)]
    [InlineData("Reports_Export", true)]
    public async Task Saved_Accountant_grants_cannot_write_outside_Finance(string permission, bool allowed)
    {
        using var f = new Fixture();
        var evaluator = new PermissionEvaluator(f.Db);
        Assert.Equal(allowed, await evaluator.HasPermissionAsync(f.Actor.Id, permission));
        Assert.Equal(allowed, (await evaluator.GetPermissionsAsync(f.Actor.Id)).Contains(permission));
    }

    [Fact]
    public async Task Another_live_role_keeps_its_own_write_authority()
    {
        using var f = new Fixture();
        var manager = new Role { Name = "CompanyAdmin", GranularPermissions = [new() { Permission = GranularPermission.Users_Edit }] };
        f.Actor.UserRoles.Add(new() { Role = manager });
        f.Db.SaveChanges(true);
        Assert.True(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Users_Edit"));
        manager.IsDeleted = true;
        f.Db.SaveChanges(true);
        Assert.False(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Users_Edit"));
    }

    [Fact]
    public async Task Matrix_does_not_present_legacy_Accountant_write_grants_as_effective()
    {
        using var f = new Fixture();
        var rows = await new RolePermissionService(f.Db).GetPermissionsAsync(f.Role.Id);
        Assert.False(rows.Single(p => p.Permission == GranularPermission.Users_Delete).Granted);
        Assert.False(rows.Single(p => p.Permission == GranularPermission.Procurement_Inventory_Adjust).Granted);
        Assert.True(rows.Single(p => p.Permission == GranularPermission.Finance_Payment_Manage).Granted);
    }

    [Fact]
    public async Task SuperAdmin_cannot_grant_Accountant_nonfinance_writes()
    {
        using var f = new Fixture();
        var admin = new User { IsActive = true, UserRoles = [new() { Role = new Role { Name = "SuperAdmin", GranularPermissions = [new() { Permission = GranularPermission.Users_Edit }] } }] };
        f.Db.Users.Add(admin);
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<ArgumentException>(() => new RolePermissionService(f.Db).UpdatePermissionsAsync(admin.Id, f.Role.Id,
            new UpdateRolePermissionsRequest([GranularPermission.Users_View, GranularPermission.Users_Delete])));
        Assert.True(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Finance_Payment_Manage"));
    }

    [Theory]
    [InlineData("/users")]
    [InlineData("/equipment/list")]
    [InlineData("/equipment/sites")]
    [InlineData("/equipment/downtime-sites")]
    [InlineData("/equipment/maintenance-sites")]
    [InlineData("/equipment/fuel-sites")]
    [InlineData("/equipment/hourmeter-sites")]
    [InlineData("/equipment/utilization-sites")]
    [InlineData("/equipment/timesheet-sites")]
    [InlineData("/materials")]
    [InlineData("/vendors")]
    [InlineData("/purchase-orders")]
    [InlineData("/goods-receipts")]
    [InlineData("/stock-balance")]
    [InlineData("/stock-movements")]
    [InlineData("/stock-issues")]
    [InlineData("/stock-returns")]
    [InlineData("/stock-adjustments")]
    [InlineData("/material-return-audit")]
    [InlineData("/daily-material-usage")]
    public void Granted_Accountant_can_reach_company_read_routes(string route)
    {
        var access = new WorkspaceAccess(["Accountant"], Enum.GetNames<GranularPermission>());
        Assert.True(access.CanVisit(route));
        Assert.False(access.CanVisit(route + "/create"));
    }

    [Theory]
    [InlineData("Users_Edit")]
    [InlineData("Users_Delete")]
    [InlineData("Procurement_Inventory_Adjust")]
    [InlineData("Procurement_PurchaseOrder_Approve")]
    public void Stale_browser_permissions_do_not_expose_Accountant_writes(string permission)
    {
        var access = new WorkspaceAccess(["Accountant"], Enum.GetNames<GranularPermission>());
        Assert.False(access.Has(permission));
        Assert.True(access.Has("Finance_Payment_Manage"));
        Assert.False(new WorkspaceAccess(["Accountant"], []).CanVisit("/equipment/list"));
    }

    [Theory]
    [InlineData(typeof(EquipmentController), "GetAll", true)]
    [InlineData(typeof(EquipmentAssignmentsController), "GetAll", true)]
    [InlineData(typeof(EquipmentMaintenanceController), "GetAll", true)]
    [InlineData(typeof(EquipmentDowntimeController), "GetAll", true)]
    [InlineData(typeof(EquipmentFuelLogsController), "GetAll", true)]
    [InlineData(typeof(EquipmentUtilizationController), "GetAll", true)]
    [InlineData(typeof(VendorsController), "GetAll", true)]
    [InlineData(typeof(PurchaseOrdersController), "GetAll", true)]
    [InlineData(typeof(UsersController), "Update", false)]
    [InlineData(typeof(UsersController), "Delete", false)]
    [InlineData(typeof(StockAdjustmentsController), "Create", false)]
    [InlineData(typeof(PurchaseOrdersController), "Approve", false)]
    [InlineData(typeof(MaterialRequestsController), "Approve", false)]
    [InlineData(typeof(EquipmentController), "Create", false)]
    [InlineData(typeof(VendorsController), "Create", false)]
    public async Task Api_authority_matches_Accountant_read_only_contract(Type controller, string action, bool allowed)
    {
        var registrations = new ServiceCollection().AddLogging();
        registrations.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var services = registrations.AddGrantedBackendPermissions("Accountant").BuildServiceProvider();
        var metadata = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var actor = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Accountant")], "test")));
        Assert.Equal(allowed, (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public async Task Granted_Accountant_without_site_assignment_can_read_service_lists(int area)
    {
        using var f = new Fixture();
        switch (area)
        {
            case 0: Assert.Empty(await new EquipmentService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 1: Assert.Empty(await new EquipmentAssignmentService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 2: Assert.Empty(await new EquipmentMaintenanceService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 3: Assert.Empty(await new EquipmentDowntimeService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 4: Assert.Empty(await new PurchaseOrderService(f.Db).GetAllAsync(f.Actor.Id, null, null, null)); break;
            case 5: Assert.Empty(await new GoodsReceiptService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 6: Assert.Empty(await new StockBalanceService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 7: Assert.Empty(await new StockMovementService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 8: Assert.Empty(await new StockIssueService(f.Db, null!).GetAllAsync(f.Actor.Id)); break;
            case 9: Assert.Empty(await new StockReturnService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 10: Assert.Empty(await new StockAdjustmentService(f.Db).GetAllAsync(f.Actor.Id)); break;
            case 11: Assert.Empty(await new VendorService(f.Db).GetAllAsync(f.Actor.Id, null)); break;
        }
    }

    [Fact]
    public async Task Equipment_reads_keep_company_boundary_and_honor_live_revocation()
    {
        using var f = new Fixture();
        var own = new Equipment { CompanyId = f.Actor.CompanyId!.Value, EquipmentCode = "OWN", Name = "Own", IsActive = true };
        var foreign = new Equipment { CompanyId = Guid.NewGuid(), EquipmentCode = "FOREIGN", Name = "Foreign", IsActive = true };
        f.Db.Equipment.AddRange(own, foreign);
        f.Db.SaveChanges(true);
        var service = new EquipmentService(f.Db);
        Assert.Equal(own.Id, Assert.Single(await service.GetAllAsync(f.Actor.Id)).Id);
        Assert.Equal(own.Id, (await service.GetByIdAsync(f.Actor.Id, own.Id)).Id);
        await Assert.ThrowsAsync<EquipmentManagementException>(() => service.GetByIdAsync(f.Actor.Id, foreign.Id));
        var grant = f.Role.GranularPermissions.Single(p => p.Permission == GranularPermission.Procurement_Inventory_View);
        f.Db.RoleGranularPermissions.Remove(grant);
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<EquipmentManagementException>(() => service.GetAllAsync(f.Actor.Id));
    }

    [Fact]
    public async Task Accountant_can_read_user_detail_in_own_company_without_management_authority()
    {
        using var f = new Fixture();
        var own = new User { CompanyId = f.Actor.CompanyId, IsActive = true, FirstName = "Own" };
        var foreign = new User { CompanyId = Guid.NewGuid(), IsActive = true, FirstName = "Foreign" };
        f.Db.Users.AddRange(own, foreign);
        f.Db.SaveChanges(true);
        var service = new UserService(f.Db, new PasswordHasher());
        Assert.Equal(own.Id, (await service.GetByIdAsync(f.Actor.Id, own.Id)).Id);
        Assert.Equal(own.Id, (await service.GetProfileAsync(f.Actor.Id, own.Id)).Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(f.Actor.Id, foreign.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(f.Actor.Id, own.Id));
        Assert.False(own.IsDeleted);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("deleted")] [InlineData("role-deleted")]
    [InlineData("membership-deleted")] [InlineData("missing-company")]
    public async Task Accountant_read_rejects_invalid_live_authority(string invalid)
    {
        using var f = new Fixture();
        if (invalid == "inactive") f.Actor.IsActive = false;
        if (invalid == "deleted") f.Actor.IsDeleted = true;
        if (invalid == "role-deleted") f.Role.IsDeleted = true;
        if (invalid == "membership-deleted") f.Actor.UserRoles.Single().IsDeleted = true;
        if (invalid == "missing-company") f.Actor.CompanyId = null;
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<EquipmentManagementException>(() => new EquipmentService(f.Db).GetAllAsync(f.Actor.Id));
    }

    [Fact]
    public async Task Startup_removes_unsupported_Accountant_grants_but_preserves_Finance()
    {
        using var f = new Fixture();
        var seed = typeof(DatabaseInitializer).GetMethod("SeedGranularRolePermissionsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)seed.Invoke(null, new object[] { f.Db, CancellationToken.None })!;
        Assert.DoesNotContain(f.Db.RoleGranularPermissions, p => p.RoleId == f.Role.Id && p.Permission == GranularPermission.Users_Delete);
        Assert.Contains(f.Db.RoleGranularPermissions, p => p.RoleId == f.Role.Id && p.Permission == GranularPermission.Finance_Payment_Manage);
        await (Task)seed.Invoke(null, new object[] { f.Db, CancellationToken.None })!;
        Assert.Single(f.Db.RoleGranularPermissions.Where(p => p.RoleId == f.Role.Id && p.Permission == GranularPermission.Procurement_PurchaseOrder_View));
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Role Role { get; } = new() { Name = "Accountant", GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() };
        public User Actor { get; }
        public Fixture()
        {
            Actor = new User { CompanyId = Guid.NewGuid(), IsActive = true, UserRoles = [new() { Role = Role }] };
            Db.Users.Add(Actor);
            Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
}
