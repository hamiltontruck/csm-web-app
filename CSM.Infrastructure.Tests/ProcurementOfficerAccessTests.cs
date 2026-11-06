using System.Reflection;
using CSM.Domain.Entities.Notifications;
using CSM.Application.Notifications.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CSM.Application.Inventory.StockAdjustments.Dtos;
using CSM.Application.Inventory.StockTransfers.Dtos;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.PurchaseOrders.Dtos;
using CSM.Application.Procurement.MaterialRequests.Dtos;
using CSM.Domain.Entities.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        Assert.Single(await new VendorService(f.Db).GetAllAsync(f.Actor.Id, null));
    }

    [Fact]
    public async Task Startup_seeds_exact_contract_and_cleans_unsupported_grants()
    {
        using var f = new Fixture();
        var seed = typeof(DatabaseInitializer).GetMethod("SeedGranularRolePermissionsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)seed.Invoke(null, new object[] { f.Db, CancellationToken.None })!;
        Assert.Equal(Allowed.Order(), f.Db.RoleGranularPermissions.Where(x => x.RoleId == f.Role.Id).Select(x => x.Permission.ToString()).Order());
    }

    [Fact]
    public async Task Officer_creates_and_submits_PO_but_cannot_approve_even_with_stale_approval_grant()
    {
        using var f = new Fixture();
        f.Assign(f.First);
        var service = new PurchaseOrderService(f.Db);
        var order = await service.CreateAsync(f.Actor.Id, f.Order(f.First));
        Assert.Equal(100m, order.TotalAmount);
        Assert.Equal(PurchaseOrderStatus.PendingApproval, (await service.SubmitAsync(f.Actor.Id, order.Id)).Status);
        await Assert.ThrowsAnyAsync<Exception>(() => service.ApproveAsync(f.Actor.Id, order.Id));
        Assert.Equal(PurchaseOrderStatus.PendingApproval, f.Db.PurchaseOrders.Single().Status);
        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync(f.Actor.Id, f.Order(f.Second)));
        Assert.Single(f.Db.PurchaseOrders);
    }

    [Fact]
    public async Task Officer_creates_and_submits_MR_in_scope_and_cannot_approve()
    {
        using var f = new Fixture();
        f.Assign(f.First);
        var service = new MaterialRequestService(f.Db);
        var mr = await service.CreateAsync(f.Actor.Id, new CSM.Application.Procurement.MaterialRequests.Dtos.CreateMaterialRequestRequest {
            ConstructionSiteId = f.First.Id, RequestNumber = "MR-OFFICER", RequestDate = new(2026, 11, 6),
            Lines = [new() { MaterialId = f.Material.Id, RequestedQuantity = 1m }] });
        Assert.Equal(MaterialRequestStatus.Submitted, (await service.SubmitAsync(f.Actor.Id, mr.Id)).Status);
        await Assert.ThrowsAnyAsync<Exception>(() => service.ApproveAsync(f.Actor.Id, mr.Id, new ApproveMaterialRequestRequest()));
        Assert.Equal(MaterialRequestStatus.Submitted, f.Db.MaterialRequests.Single().Status);
    }

    [Theory]
    [InlineData(typeof(EquipmentFuelLogsController), "GetAll")]
    [InlineData(typeof(EquipmentFuelLogsController), "Create")]
    [InlineData(typeof(EquipmentUtilizationController), "GetAll")]
    [InlineData(typeof(EquipmentHourLogsController), "GetAll")]
    [InlineData(typeof(EquipmentHourLogsController), "Create")]
    [InlineData(typeof(EquipmentTimesheetsController), "GetAll")]
    [InlineData(typeof(EquipmentTimesheetsController), "Create")]
    public async Task Officer_inventory_grant_does_not_open_raw_equipment_APIs(Type controller, string action)
    {
        var registrations = new ServiceCollection().AddLogging();
        registrations.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = registrations.AddGrantedBackendPermissions(AppRoles.ProcurementOfficer).BuildServiceProvider();
        var attributes = controller.GetCustomAttributes<AuthorizeAttribute>().Concat(controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);
        var actor = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, AppRoles.ProcurementOfficer)], "test")));
        Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
    }

    [Fact]
    public async Task Dashboard_and_reports_follow_conditional_scope_without_financial_leak()
    {
        using var f = new Fixture();
        f.Db.Companies.Add(new Company { Id = f.Actor.CompanyId!.Value, Name = "Own", Code = "OWN", IsActive = true });
        f.Db.SaveChanges(true);
        var service = new WorkspaceDashboardService(f.Db);
        Assert.Equal(2, (await service.GetAsync(f.Actor.Id)).LowStockItems);
        f.Assign(f.First);
        var dashboard = await service.GetAsync(f.Actor.Id);
        Assert.Equal(1, dashboard.LowStockItems);
        Assert.Null(dashboard.TotalBudget);
        Assert.Null(dashboard.TotalExpenses);
        var report = await new ReportingService(f.Db).GetExecutiveDashboardAsync(f.Actor.Id);
        Assert.Equal(1, report.TotalSites);
        Assert.Null(report.TotalBudget);
        await Assert.ThrowsAnyAsync<Exception>(() => new ReportingService(f.Db).GetFinancialSummaryAsync(f.Actor.Id));
    }

    [Fact]
    public async Task Stock_adjustment_checks_assignment_before_any_balance_or_movement_change()
    {
        using var f = new Fixture();
        f.Assign(f.First);
        var service = new StockAdjustmentService(f.Db);
        var denied = new CreateStockAdjustmentRequest { ConstructionSiteId = f.Second.Id, MaterialId = f.Material.Id,
            Increase = true, Quantity = 2m, AdjustmentNumber = "DENIED", Reason = "Test", AdjustmentDateUtc = DateTime.UtcNow };
        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync(f.Actor.Id, denied));
        Assert.Equal(0m, f.SecondStock.QuantityOnHand);
        Assert.Empty(f.Db.StockMovements);
        denied.ConstructionSiteId = f.First.Id;
        denied.AdjustmentNumber = "ALLOWED";
        await service.CreateAsync(f.Actor.Id, denied);
        Assert.Equal(2m, f.Db.StockItems.Single(x => x.ConstructionSiteId == f.First.Id).QuantityOnHand);
        Assert.Single(f.Db.StockMovements);
    }

    [Fact]
    public async Task Public_transfer_list_requires_both_sites_and_never_returns_another_company()
    {
        using var f = new Fixture();
        var first = f.Db.StockItems.Single(x => x.ConstructionSiteId == f.First.Id);
        f.Db.StockMovements.AddRange(
            new StockMovement { CompanyId = f.First.CompanyId, StockItemId = first.Id, MovementType = StockMovementType.TransferOut, ReferenceType = "StockTransfer", ReferenceNumber = "PAIR", Quantity = 1m },
            new StockMovement { CompanyId = f.Second.CompanyId, StockItemId = f.SecondStock.Id, MovementType = StockMovementType.TransferIn, ReferenceType = "StockTransfer", ReferenceNumber = "PAIR", Quantity = 1m });
        var foreignMaterial = new Material { CompanyId = f.Foreign.CompanyId, MaterialCode = "F", Name = "Foreign", UnitOfMeasure = "Bag" };
        var foreignStock = new StockItem { CompanyId = f.Foreign.CompanyId, ConstructionSite = f.Foreign, Material = foreignMaterial };
        f.Db.StockItems.Add(foreignStock);
        f.Db.StockMovements.Add(new StockMovement { CompanyId = f.Foreign.CompanyId, StockItem = foreignStock, MovementType = StockMovementType.TransferOut, ReferenceType = "StockTransfer", ReferenceNumber = "FOREIGN", Quantity = 1m });
        f.Db.SaveChanges(true);
        var controller = TransferController(f);
        var own = Assert.IsType<OkObjectResult>((await controller.GetAll(CancellationToken.None)).Result);
        Assert.Single(Assert.IsType<List<StockTransferResponse>>(own.Value));
        f.Assign(f.First);
        var assigned = Assert.IsType<OkObjectResult>((await controller.GetAll(CancellationToken.None)).Result);
        Assert.Empty(Assert.IsType<List<StockTransferResponse>>(assigned.Value));
        f.Assign(f.Second);
        var both = Assert.IsType<OkObjectResult>((await controller.GetAll(CancellationToken.None)).Result);
        Assert.Single(Assert.IsType<List<StockTransferResponse>>(both.Value));
    }

    [Fact]
    public async Task Public_transfer_create_rejects_unassigned_destination_before_writing()
    {
        using var f = new Fixture();
        f.Assign(f.First);
        var source = f.Db.StockItems.Single(x => x.ConstructionSiteId == f.First.Id);
        source.QuantityOnHand = 10m;
        f.Db.SaveChanges(true);
        var controller = TransferController(f);
        var result = await controller.Create(new CreateStockTransferRequest { SourceConstructionSiteId = f.First.Id,
            DestinationConstructionSiteId = f.Second.Id, MaterialId = f.Material.Id, Quantity = 1m,
            TransferNumber = "DENIED", TransferDateUtc = DateTime.UtcNow }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(10m, source.QuantityOnHand);
        Assert.Equal(0m, f.SecondStock.QuantityOnHand);
        Assert.Empty(f.Db.StockMovements);
    }

    private static StockTransfersController TransferController(Fixture f) => new(new StockTransferService(f.Db)) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, f.Actor.Id.ToString()), new Claim("company_id", f.First.CompanyId.ToString())], "test")) } } };

    [Fact]
    public async Task Assignment_change_hides_out_of_scope_material_request_notifications_and_counts()
    {
        using var f = new Fixture();
        f.Assign(f.First);
        var request = new MaterialRequest { CompanyId = f.Second.CompanyId, ConstructionSite = f.Second,
            RequestNumber = "HIDDEN", RequestDate = new(2026, 11, 6), RequestedBy = f.Actor.Id };
        var notification = new Notification { CompanyId = f.Second.CompanyId, RecipientUserId = f.Actor.Id,
            Title = "Material Request", Message = "Other site details", SourceEntityType = "MaterialRequest", SourceEntityId = request.Id };
        f.Db.MaterialRequests.Add(request); f.Db.Notifications.Add(notification); f.Db.SaveChanges(true);
        var service = new NotificationService(f.Db);
        Assert.Empty(await service.GetAllAsync(f.Actor.Id, null));
        Assert.Equal(0, await service.GetUnreadCountAsync(f.Actor.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => service.MarkReadAsync(f.Actor.Id, notification.Id, new MarkNotificationReadRequest(true)));
        Assert.False(notification.IsRead);
    }

    private sealed class Fixture : IDisposable
    {
        public TestDb Db { get; } = new();
        public Role Role { get; } = new() { Name = AppRoles.ProcurementOfficer, GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() };
        public User Actor { get; }
        public ConstructionSite First { get; }
        public ConstructionSite Second { get; }
        public ConstructionSite Foreign { get; }
        public StockItem SecondStock { get; }
        public Material Material { get; }
        public Vendor Vendor { get; }
        public Fixture()
        {
            var company = Guid.NewGuid();
            Actor = new User { CompanyId = company, IsActive = true, UserRoles = [new() { Role = Role }] };
            First = new() { CompanyId = company, Name = "First", SiteCode = "FIRST", IsActive = true };
            Second = new() { CompanyId = company, Name = "Second", SiteCode = "SECOND", IsActive = true };
            Foreign = new() { CompanyId = Guid.NewGuid(), Name = "Foreign", SiteCode = "FOREIGN", IsActive = true };
            Material = new Material { CompanyId = company, MaterialCode = "M", Name = "Material", UnitOfMeasure = "Bag", IsActive = true };
            Vendor = new Vendor { CompanyId = company, VendorCode = "V", Name = "Vendor", IsActive = true };
            Db.Vendors.Add(Vendor);
            SecondStock = new() { CompanyId = company, ConstructionSite = Second, Material = Material };
            Db.Users.Add(Actor);
            Db.ConstructionSites.AddRange(First, Second, Foreign);
            Db.StockItems.AddRange(new StockItem { CompanyId = company, ConstructionSite = First, Material = Material }, SecondStock);
            Db.SaveChanges(true);
        }
        public CreatePurchaseOrderRequest Order(ConstructionSite site) => new() {
            ConstructionSiteId = site.Id, VendorId = Vendor.Id, PurchaseOrderNumber = "PO-" + site.SiteCode,
            OrderDate = new(2026, 11, 6), Lines = [new() { MaterialId = Material.Id, OrderedQuantity = 2m, UnitPrice = 50m }] };
        public UserSiteAccess Assign(ConstructionSite site)
        {
            var access = new UserSiteAccess { UserId = Actor.Id, SiteId = site.Id, CompanyId = site.CompanyId, IsActive = true };
            Db.UserSiteAccesses.Add(access);
            Db.SaveChanges(true);
            return access;
        }
        public void Dispose() => Db.Dispose();
    }
    private sealed class TestDb() : ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
