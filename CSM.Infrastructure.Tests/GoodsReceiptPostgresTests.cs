using CSM.Application.Procurement.GoodsReceipts.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class GoodsReceiptPostgresTests
{
 [Theory]
 [InlineData(DateTimeKind.Unspecified)] [InlineData(DateTimeKind.Local)] [InlineData(DateTimeKind.Utc)]
 public async Task Receipt_date_and_stock_movement_persist_as_UTC_without_double_receiving(DateTimeKind kind)
 {
  await using var fixture=await FinanceSettlementPostgresFixture.CreateAsync();await using var db=fixture.Context();
  var company=new Company{Code="GR",Name="Receipt company",IsActive=true};var site=new ConstructionSite{CompanyId=company.Id,SiteCode="GR-SITE",Name="Receipt site",IsActive=true};
  var vendor=new Vendor{CompanyId=company.Id,VendorCode="VENDOR",Name="Vendor"};var material=new Material{CompanyId=company.Id,MaterialCode="MAT",Name="Cement",Category="General",UnitOfMeasure="Bag",IsActive=true};
  var actor=new User{CompanyId=null,Email="receipt@example.test",NormalizedEmail="RECEIPT@EXAMPLE.TEST",IsActive=true,UserRoles=[new(){Role=FinanceOfficerAuthorityTests.Role("SuperAdmin",GranularPermission.Procurement_Inventory_View,GranularPermission.Procurement_Inventory_Adjust)}]};
  var order=new PurchaseOrder{CompanyId=company.Id,ConstructionSiteId=site.Id,VendorId=vendor.Id,PurchaseOrderNumber="PO-LOCAL",OrderDate=new(2026,11,4),Status=PurchaseOrderStatus.SentToVendor};
  var line=new PurchaseOrderLine{CompanyId=company.Id,PurchaseOrderId=order.Id,MaterialId=material.Id,OrderedQuantity=10,UnitPrice=5};order.Lines.Add(line);
  db.AddRange(company,site,vendor,material,actor,order);await db.SaveChangesAsync();
  var date=new DateTime(2026,11,4,0,0,0,kind);var expected=kind==DateTimeKind.Local?date.ToUniversalTime():DateTime.SpecifyKind(date,DateTimeKind.Utc);
  var request=new CreateGoodsReceiptRequest{PurchaseOrderId=order.Id,ConstructionSiteId=site.Id,ReceiptNumber="GR-005",ReceivedAtUtc=date,Lines=[new(){PurchaseOrderLineId=line.Id,ReceivedQuantity=2,AcceptedQuantity=2}]};
  await new GoodsReceiptService(db).CreateAsync(actor.Id,request);
  await using var verify=fixture.Context();var receipt=await verify.GoodsReceipts.SingleAsync();var movement=await verify.StockMovements.SingleAsync();
  Assert.Equal(DateTimeKind.Utc,receipt.ReceivedAtUtc.Kind);Assert.Equal(expected,receipt.ReceivedAtUtc);Assert.Equal(expected,movement.MovementDateUtc);Assert.Equal(DateTimeKind.Utc,movement.MovementDateUtc.Kind);
  Assert.Equal(2,(await verify.StockItems.SingleAsync()).QuantityOnHand);Assert.Equal(2,(await verify.PurchaseOrderLines.SingleAsync()).ReceivedQuantity);
  await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.GoodsReceiptManagementException>(()=>new GoodsReceiptService(db).CreateAsync(actor.Id,request));
  Assert.Single(await verify.GoodsReceipts.ToListAsync());Assert.Single(await verify.StockMovements.ToListAsync());
 }
}
