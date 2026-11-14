using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockTransfers.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using CSM.Web.Services.Access;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class SuperAdminStockTransferTests
{
 [Theory] [InlineData(false)] [InlineData(true)]
 public void Superadmin_create_route_requires_adjust_permission(bool adjust)
 {
  var grants=new List<string>{"Sites_View","Procurement_Inventory_View"};if(adjust)grants.Add("Procurement_Inventory_Adjust");
  Assert.Equal(adjust,new WorkspaceAccess(["SuperAdmin"],grants).CanVisit("/stock-transfers/create/11111111-1111-1111-1111-111111111111"));
 }
 [Theory] [InlineData(false)] [InlineData(true)]
 public async Task Superadmin_transfers_with_source_company_and_keeps_profile(bool assigned)
 {
  await using var f=await FinanceSettlementPostgresFixture.CreateAsync();await using var db=f.Context();
  var a=new Company{Code="A",Name="Profile company",IsActive=true};var b=new Company{Code="B",Name="Stock company",IsActive=true};
  var actor=new User{CompanyId=assigned?a.Id:null,Email="transfer@example.test",NormalizedEmail="TRANSFER@EXAMPLE.TEST",IsActive=true,UserRoles=[new(){Role=FinanceOfficerAuthorityTests.Role("SuperAdmin",GranularPermission.Procurement_Inventory_View,GranularPermission.Procurement_Inventory_Adjust)}]};
  var from=new ConstructionSite{CompanyId=b.Id,SiteCode="FROM",Name="Source",IsActive=true};var to=new ConstructionSite{CompanyId=b.Id,SiteCode="TO",Name="Destination",IsActive=true};var foreign=new ConstructionSite{CompanyId=a.Id,SiteCode="FOREIGN",Name="Foreign",IsActive=true};
  var mat=new Material{CompanyId=b.Id,MaterialCode="CEMENT",Name="Cement",UnitOfMeasure="Bag",Category="General",IsActive=true};
  var source=new StockItem{CompanyId=b.Id,ConstructionSiteId=from.Id,MaterialId=mat.Id,QuantityOnHand=5,AverageUnitCost=3};var dest=new StockItem{CompanyId=b.Id,ConstructionSiteId=to.Id,MaterialId=mat.Id,QuantityOnHand=1,AverageUnitCost=3};
  db.AddRange(a,b,actor,from,to,foreign,mat,source,dest);await db.SaveChangesAsync();
  var service=new StockTransferService(db);var request=new CreateStockTransferRequest{SourceConstructionSiteId=from.Id,DestinationConstructionSiteId=to.Id,MaterialId=mat.Id,Quantity=1,TransferDateUtc=DateTime.UtcNow,TransferNumber="TRANSFER-LOCAL"};
  await service.CreateForUserAsync(actor.Id,request);
  await using var verify=f.Context();Assert.Equal(4,(await verify.StockItems.SingleAsync(x=>x.Id==source.Id)).QuantityOnHand);Assert.Equal(2,(await verify.StockItems.SingleAsync(x=>x.Id==dest.Id)).QuantityOnHand);Assert.Equal(2,await verify.StockMovements.CountAsync());Assert.All(await verify.StockMovements.ToListAsync(),x=>Assert.Equal(b.Id,x.CompanyId));Assert.Equal(assigned?a.Id:(Guid?)null,(await verify.Users.SingleAsync()).CompanyId);
  request.DestinationConstructionSiteId=foreign.Id;request.TransferNumber="FOREIGN";await Assert.ThrowsAsync<StockTransferManagementException>(()=>service.CreateForUserAsync(actor.Id,request));Assert.Equal(2,await verify.StockMovements.CountAsync());
  db.Remove(actor.UserRoles.Single().Role.GranularPermissions.Single(x=>x.Permission==GranularPermission.Procurement_Inventory_Adjust));await db.SaveChangesAsync();request.DestinationConstructionSiteId=to.Id;
  await Assert.ThrowsAsync<StockTransferManagementException>(()=>service.CreateForUserAsync(actor.Id,request));Assert.Equal(2,await verify.StockMovements.CountAsync());
 }
}
