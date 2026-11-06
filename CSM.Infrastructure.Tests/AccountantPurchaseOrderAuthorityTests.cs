using System.Reflection;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.PurchaseOrders.Dtos;
using CSM.Application.Roles.Dtos;
using CSM.Domain.Entities.Finance;
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

public sealed class AccountantPurchaseOrderAuthorityTests
{
    [Fact]
    public async Task Accountant_manages_and_approves_own_company_PO_without_site_assignment()
    {
        using var f = new Fixture();
        var service = new PurchaseOrderService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id, f.Request());
        Assert.Equal(100m, created.TotalAmount);
        Assert.Equal("Draft", created.Status.ToString());
        var changed = await service.UpdateAsync(f.Actor.Id, created.Id, new UpdatePurchaseOrderRequest {
            ConstructionSiteId = f.Site.Id, VendorId = f.Vendor.Id, PurchaseOrderNumber = "ACC-PO-TEST-001",
            OrderDate = new DateOnly(2026, 11, 5), Notes = "Accountant PO note.",
            Lines = [new() { MaterialId = f.Material.Id, OrderedQuantity = 2m, UnitPrice = 50m }] });
        Assert.Equal("Accountant PO note.", changed.Notes);
        Assert.Equal(100m, changed.TotalAmount);
        Assert.Equal("PendingApproval", (await service.SubmitAsync(f.Actor.Id, created.Id)).Status.ToString());
        var approved = await service.ApproveAsync(f.Actor.Id, created.Id);
        Assert.Equal("Approved", approved.Status.ToString());
        Assert.Equal(f.Actor.Id, f.Db.PurchaseOrders.Single().ApprovedBy);
        Assert.Equal("SentToVendor", (await service.SendToVendorAsync(f.Actor.Id, created.Id)).Status.ToString());
        Assert.Equal("Cancelled", (await service.CancelAsync(f.Actor.Id, created.Id)).Status.ToString());
        Assert.Empty(f.Db.UserSiteAccesses);
        Assert.False(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Procurement_Inventory_Adjust"));
        Assert.False(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Users_View"));
        Assert.True(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Finance_Payment_Manage"));
    }

    [Theory]
    [InlineData("manage-revoked")] [InlineData("approve-revoked")]
    [InlineData("inactive")] [InlineData("role-deleted")]
    [InlineData("membership-deleted")] [InlineData("missing-company")]
    public async Task PO_mutation_requires_live_Accountant_action_grant(string denied)
    {
        using var f = new Fixture();
        var record = f.Record(denied == "approve-revoked" ? PurchaseOrderStatus.PendingApproval : PurchaseOrderStatus.Draft);
        f.Db.PurchaseOrders.Add(record);
        if (denied.EndsWith("revoked")) f.Db.RoleGranularPermissions.Remove(f.Role.GranularPermissions.Single(p => p.Permission ==
            (denied == "approve-revoked" ? GranularPermission.Procurement_PurchaseOrder_Approve : GranularPermission.Procurement_PurchaseOrder_Manage)));
        if (denied == "inactive") f.Actor.IsActive = false;
        if (denied == "role-deleted") f.Role.IsDeleted = true;
        if (denied == "membership-deleted") f.Actor.UserRoles.Single().IsDeleted = true;
        if (denied == "missing-company") f.Actor.CompanyId = null;
        f.Db.SaveChanges(true);
        var service = new PurchaseOrderService(f.Db);
        var before = record.Status;
        await Assert.ThrowsAsync<PurchaseOrderManagementException>(() => denied == "approve-revoked"
            ? service.ApproveAsync(f.Actor.Id, record.Id) : service.SubmitAsync(f.Actor.Id, record.Id));
        Assert.Equal(before, record.Status);
        Assert.Null(record.ApprovedBy);
    }

    [Fact]
    public async Task PO_create_and_approve_cannot_cross_company_boundary()
    {
        using var f = new Fixture();
        var foreignSite = new ConstructionSite { CompanyId = Guid.NewGuid(), Name = "Foreign", SiteCode = "FOREIGN", IsActive = true };
        f.Db.ConstructionSites.Add(foreignSite);
        var foreignOrder = f.Record(PurchaseOrderStatus.PendingApproval);
        foreignOrder.CompanyId = foreignSite.CompanyId;
        foreignOrder.ConstructionSite = foreignSite;
        foreignOrder.ConstructionSiteId = foreignSite.Id;
        f.Db.PurchaseOrders.Add(foreignOrder);
        f.Db.SaveChanges(true);
        var service = new PurchaseOrderService(f.Db);
        var request = f.Request(); request.ConstructionSiteId = foreignSite.Id;
        await Assert.ThrowsAsync<PurchaseOrderManagementException>(() => service.CreateAsync(f.Actor.Id, request));
        await Assert.ThrowsAsync<PurchaseOrderManagementException>(() => service.ApproveAsync(f.Actor.Id, foreignOrder.Id));
        Assert.Equal(PurchaseOrderStatus.PendingApproval, foreignOrder.Status);
        Assert.Null(foreignOrder.ApprovedBy);
        Assert.Single(f.Db.PurchaseOrders);
    }

    [Fact]
    public async Task Matrix_allows_PO_actions_and_rejects_user_access_for_Accountant()
    {
        using var f = new Fixture();
        var admin = new User { IsActive = true, UserRoles = [new() { Role = new Role { Name = "SuperAdmin", GranularPermissions = [new() { Permission = GranularPermission.Users_Edit }] } }] };
        f.Db.Users.Add(admin); f.Db.SaveChanges(true);
        var service = new RolePermissionService(f.Db);
        var rows = await service.GetPermissionsAsync(f.Role.Id);
        Assert.False(rows.Single(p => p.Permission == GranularPermission.Users_View).Granted);
        Assert.True(rows.Single(p => p.Permission == GranularPermission.Procurement_PurchaseOrder_Manage).Granted);
        await service.UpdatePermissionsAsync(admin.Id, f.Role.Id, new UpdateRolePermissionsRequest([
            GranularPermission.Procurement_PurchaseOrder_View, GranularPermission.Procurement_PurchaseOrder_Manage,
            GranularPermission.Procurement_PurchaseOrder_Approve, GranularPermission.Finance_Payment_Manage]));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePermissionsAsync(admin.Id, f.Role.Id,
            new UpdateRolePermissionsRequest([GranularPermission.Users_View])));
        Assert.True(await new PermissionEvaluator(f.Db).HasPermissionAsync(f.Actor.Id, "Finance_Payment_Manage"));
    }

    [Fact]
    public async Task Startup_adds_PO_authority_and_removes_old_user_grants()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.RemoveRange(f.Role.GranularPermissions.Where(p => p.Permission is GranularPermission.Procurement_PurchaseOrder_Manage or GranularPermission.Procurement_PurchaseOrder_Approve));
        f.Db.SaveChanges(true);
        var seed = typeof(DatabaseInitializer).GetMethod("SeedGranularRolePermissionsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)seed.Invoke(null, new object[] { f.Db, CancellationToken.None })!;
        Assert.DoesNotContain(f.Db.RoleGranularPermissions, p => p.RoleId == f.Role.Id && p.Permission == GranularPermission.Users_View);
        Assert.Contains(f.Db.RoleGranularPermissions, p => p.RoleId == f.Role.Id && p.Permission == GranularPermission.Procurement_PurchaseOrder_Manage);
        Assert.Contains(f.Db.RoleGranularPermissions, p => p.RoleId == f.Role.Id && p.Permission == GranularPermission.Procurement_PurchaseOrder_Approve);
    }

    [Fact]
    public void PO_manage_and_approve_are_independent_and_Users_stays_hidden_with_old_grants()
    {
        var read = new WorkspaceAccess(["Accountant"], ["Sites_View", "Procurement_PurchaseOrder_View", "Users_View"]);
        Assert.True(read.CanVisit("/purchase-orders"));
        Assert.False(read.CanVisit("/purchase-orders/create/00000000-0000-0000-0000-000000000001"));
        Assert.False(read.Has("Procurement_PurchaseOrder_Approve"));
        var granted = new WorkspaceAccess(["Accountant"], Enum.GetNames<GranularPermission>());
        Assert.True(granted.CanVisit("/purchase-orders/create/00000000-0000-0000-0000-000000000001"));
        Assert.True(granted.Has("Procurement_PurchaseOrder_Approve"));
        Assert.False(granted.CanVisit("/users"));
        Assert.False(granted.CanVisit("/users/audit"));
        Assert.False(granted.Has("Users_View"));
    }

    private sealed class Fixture : IDisposable
    {
        public TestDb Db { get; } = new();
        public Role Role { get; } = new() { Name = "Accountant", GranularPermissions = new[] {
            GranularPermission.Sites_View, GranularPermission.Procurement_PurchaseOrder_View,
            GranularPermission.Procurement_PurchaseOrder_Manage, GranularPermission.Procurement_PurchaseOrder_Approve,
            GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_Adjust,
            GranularPermission.Users_View, GranularPermission.Users_Edit, GranularPermission.Finance_Payment_Manage
        }.Select(p => new RoleGranularPermission { Permission = p }).ToList() };
        public User Actor { get; }
        public ConstructionSite Site { get; }
        public Material Material { get; }
        public Vendor Vendor { get; }
        public Fixture()
        {
            var company = Guid.NewGuid();
            Actor = new User { CompanyId = company, IsActive = true, UserRoles = [new() { Role = Role }] };
            Site = new() { CompanyId = company, SiteCode = "OWN", Name = "Own Site", IsActive = true };
            Material = new() { CompanyId = company, MaterialCode = "MAT", Name = "Cement", Category = "General", UnitOfMeasure = "Bag", IsActive = true };
            Vendor = new() { CompanyId = company, VendorCode = "VEN", Name = "Supplier", IsActive = true };
            Db.Users.Add(Actor); Db.ConstructionSites.Add(Site); Db.Materials.Add(Material); Db.Vendors.Add(Vendor);
            Db.SaveChanges(true);
        }
        public CreatePurchaseOrderRequest Request() => new() { ConstructionSiteId = Site.Id, VendorId = Vendor.Id,
            PurchaseOrderNumber = "ACC-PO-TEST-001", OrderDate = new(2026, 11, 5),
            Lines = [new() { MaterialId = Material.Id, OrderedQuantity = 2m, UnitPrice = 50m }] };
        public PurchaseOrder Record(PurchaseOrderStatus status) => new() { CompanyId = Site.CompanyId,
            ConstructionSite = Site, ConstructionSiteId = Site.Id, Vendor = Vendor, VendorId = Vendor.Id,
            PurchaseOrderNumber = "EXISTING", OrderDate = new(2026, 11, 5), Status = status,
            Lines = [new() { CompanyId = Site.CompanyId, Material = Material, MaterialId = Material.Id, OrderedQuantity = 2m, UnitPrice = 50m }] };
        public void Dispose() => Db.Dispose();
    }
    private sealed class TestDb() : ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
