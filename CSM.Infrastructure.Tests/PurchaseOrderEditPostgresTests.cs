using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.PurchaseOrders.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class PurchaseOrderEditPostgresTests
{
    [Fact]
    public async Task Companyless_superadmin_updates_draft_in_place_without_reopening_approved_orders()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync();
        await using var db = fixture.Context();
        var company = new Company { Code = "POEDIT", Name = "Edit company", IsActive = true };
        var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "SITE", Name = "Site", IsActive = true };
        var vendor = new Vendor { CompanyId = company.Id, VendorCode = "VEN", Name = "Supplier", IsActive = true };
        var material = new Material { CompanyId = company.Id, MaterialCode = "MAT", Name = "Cement", Category = "General", UnitOfMeasure = "Bag", IsActive = true };
        var code = new CostCode { CompanyId = company.Id, Code = "CODE", Name = "Construction", IsActive = true };
        var role = FinanceOfficerAuthorityTests.Role("SuperAdmin", GranularPermission.Procurement_PurchaseOrder_View, GranularPermission.Procurement_PurchaseOrder_Manage);
        var actor = new User { CompanyId = null, Email = "poedit@example.test", NormalizedEmail = "POEDIT@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = role }] };
        var order = new PurchaseOrder { CompanyId = company.Id, ConstructionSiteId = site.Id, VendorId = vendor.Id, PurchaseOrderNumber = "PO-006", OrderDate = new(2026, 11, 2), Status = PurchaseOrderStatus.Draft };
        var line = new PurchaseOrderLine { CompanyId = company.Id, PurchaseOrderId = order.Id, MaterialId = material.Id, CostCodeId = code.Id, OrderedQuantity = 1000, UnitPrice = 0 };
        order.Lines.Add(line);
        db.AddRange(company, site, vendor, material, code, actor, order);
        await db.SaveChangesAsync();
        var request = new UpdatePurchaseOrderRequest { ConstructionSiteId = site.Id, VendorId = vendor.Id, PurchaseOrderNumber = "PO-006", OrderDate = order.OrderDate,
            CurrencyCode = "USD", ExchangeRate = 2, DiscountAmount = 1, ExpectedDeliveryDate = new(2026, 11, 20), DeliveryAddress = "Warehouse", PaymentTerms = "Net30", Notes = "Updated",
            Lines = [new() { MaterialId = material.Id, CostCodeId = code.Id, OrderedQuantity = 3, UnitPrice = 12.5m, TaxAmount = 3 }] };
        var result = await new PurchaseOrderService(db).UpdateAsync(actor.Id, order.Id, request);
        Assert.Equal(39.5m, result.TotalAmount);
        await using (var check = fixture.Context())
        {
            var saved = await check.PurchaseOrders.Include(x => x.Lines).SingleAsync();
            Assert.Equal(order.Id, saved.Id); Assert.Equal(PurchaseOrderStatus.Draft, saved.Status);
            var savedLine = Assert.Single(saved.Lines); Assert.Equal(line.Id, savedLine.Id); Assert.Equal(code.Id, savedLine.CostCodeId); Assert.Equal(3, savedLine.TaxAmount);
            Assert.Equal("USD", saved.CurrencyCode); Assert.Equal(2, saved.ExchangeRate); Assert.Equal("Warehouse", saved.DeliveryAddress);
        }
        foreach (var status in new[] { PurchaseOrderStatus.PendingApproval, PurchaseOrderStatus.Approved, PurchaseOrderStatus.SentToVendor })
        {
            order.Status = status; await db.SaveChangesAsync(); request.Notes = "Must not persist";
            var error = await Assert.ThrowsAsync<PurchaseOrderManagementException>(() => new PurchaseOrderService(db).UpdateAsync(actor.Id, order.Id, request));
            Assert.Contains("Only Draft", error.Message);
        }
        order.Status = PurchaseOrderStatus.Draft;
        db.RoleGranularPermissions.Remove(role.GranularPermissions.Single(x => x.Permission == GranularPermission.Procurement_PurchaseOrder_Manage));
        await db.SaveChangesAsync();
        // The controller uses this live permission evaluator before entering the service.
        Assert.False(await new PermissionEvaluator(db).HasPermissionAsync(actor.Id, "Procurement_PurchaseOrder_Manage"));
        await using var final = fixture.Context();
        Assert.Equal("Updated", (await final.PurchaseOrders.SingleAsync()).Notes);
        Assert.Equal(3, (await final.PurchaseOrderLines.SingleAsync()).OrderedQuantity);
    }
}
