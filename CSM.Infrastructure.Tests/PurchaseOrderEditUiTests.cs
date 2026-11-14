using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Domain.Enums;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class PurchaseOrderEditUiTests
{
 [Theory] [InlineData("Draft",true,true)] [InlineData("Draft",false,false)] [InlineData("PendingApproval",true,false)] [InlineData("Approved",true,false)] [InlineData("SentToVendor",true,false)] [InlineData("PartiallyDelivered",true,false)]
 public void Details_offer_edit_only_for_granted_draft(string status,bool manage,bool expected)
 {
  using var c=Context(out var api,manage);api.Order.Status=status;var p=c.RenderComponent<PurchaseOrderDetails>(x=>x.Add(y=>y.Id,api.Id));p.WaitForAssertion(()=>Assert.Contains("PO-006",p.Markup));Assert.Equal(expected,p.FindAll("button").Any(x=>x.TextContent.Trim()=="Edit"));
 }
 [Fact]
 public void Edit_prefills_and_sends_updates_without_wiping_unedited_fields()
 {
  using var c=Context(out var api);var p=Editor(c,api.Id);p.WaitForAssertion(()=>Assert.Single(p.FindAll("form")));
  Assert.Equal("PO-006",Field(p,"PO Number").GetAttribute("value"));Assert.Equal("2",Field(p,"Quantity").GetAttribute("value"));
  Field(p,"Quantity").Input("3");Field(p,"Unit Price").Input("12.50");Field(p,"Remarks").Input("Updated remark");p.Find("form").Submit();p.WaitForAssertion(()=>Assert.Equal(1,api.Updates));
  var dto=JsonSerializer.Deserialize<CSM.Application.Procurement.PurchaseOrders.Dtos.UpdatePurchaseOrderRequest>(api.Saved!,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
  Assert.Equal(api.Id,api.SavedId);Assert.Equal(api.Site,dto.ConstructionSiteId);Assert.Equal(api.Vendor,dto.VendorId);Assert.Equal(api.Project,dto.ProjectId);Assert.Equal(api.Request,dto.MaterialRequestId);Assert.Equal(new DateOnly(2026,11,20),dto.ExpectedDeliveryDate);Assert.Equal("USD",dto.CurrencyCode);Assert.Equal(2,dto.ExchangeRate);Assert.Equal(1,dto.DiscountAmount);Assert.Equal("Warehouse",dto.DeliveryAddress);Assert.Equal("Net30",dto.PaymentTerms);Assert.Equal("Updated remark",dto.Notes);var line=Assert.Single(dto.Lines);Assert.Equal(3,line.OrderedQuantity);Assert.Equal(12.5m,line.UnitPrice);Assert.Equal(3,line.TaxAmount);Assert.Equal(api.Code,line.CostCodeId);Assert.Equal(0,api.Creates);
 }
 [Theory] [InlineData("Approved")] [InlineData("PendingApproval")] [InlineData("SentToVendor")]
 public void Direct_non_draft_edit_does_not_expose_form(string status)
 {
  using var c=Context(out var api);api.Order.Status=status;var p=Editor(c,api.Id);p.WaitForAssertion(()=>Assert.Contains("Only Draft",p.Markup));Assert.Empty(p.FindAll("form"));Assert.Equal(0,api.Updates);
 }
 [Fact]
 public void Failed_update_retains_input_and_can_retry()
 {
  using var c=Context(out var api);var p=Editor(c,api.Id);p.WaitForAssertion(()=>Assert.Single(p.FindAll("form")));Field(p,"Quantity").Input("4");api.Fail=true;p.Find("form").Submit();p.WaitForAssertion(()=>Assert.Single(p.FindAll("[role=alert]")));Assert.Equal("4",Field(p,"Quantity").GetAttribute("value"));Assert.DoesNotContain("private diagnostic",p.Markup);api.Fail=false;p.Find("form").Submit();p.WaitForAssertion(()=>Assert.Equal(2,api.Updates));
 }
 [Fact]
 public void Direct_edit_requires_manage_grant()
 {
  using var c=Context(out var api,false);var p=Editor(c,api.Id);p.WaitForAssertion(()=>Assert.Contains("permission",p.Markup));Assert.Empty(p.FindAll("form"));Assert.Equal(0,api.Updates);
 }
 [Fact]
 public void Mismatched_loaded_order_is_not_editable()
 {
  using var c=Context(out var api);var p=Editor(c,Guid.NewGuid());p.WaitForAssertion(()=>Assert.Contains("not found",p.Markup));Assert.Empty(p.FindAll("form"));Assert.Equal(0,api.Updates);
 }
 [Fact]
 public void Invalid_typed_number_blocks_update()
 {
  using var c=Context(out var api);var p=Editor(c,api.Id);p.WaitForAssertion(()=>Assert.Single(p.FindAll("form")));Field(p,"Quantity").Input("invalid");p.Find("form").Submit();Assert.Equal(0,api.Updates);Assert.Contains("Quantity",p.Find(".validation-errors").TextContent);
 }
 private static AngleSharp.Dom.IElement Field(IRenderedFragment p,string name)=>p.FindAll("label").First(x=>x.TextContent.Trim().StartsWith(name,StringComparison.Ordinal)).ParentElement!.QuerySelector("input,select,textarea")!;
 private static IRenderedFragment Editor(TestContext c,Guid id){var type=typeof(PurchaseOrderCreate).Assembly.GetType("CSM.Web.Components.Pages.PurchaseOrderEdit");Assert.NotNull(type);return c.Render(b=>{b.OpenComponent(0,type);b.AddAttribute(1,"Id",id);b.CloseComponent();});}
 private static TestContext Context(out Api api,bool manage=true){var c=new TestContext();var grants=Enum.GetNames<GranularPermission>().Where(x=>manage||x!="Procurement_PurchaseOrder_Manage").ToArray();WorkspaceTestAccess.Add(c,["SuperAdmin"],grants);var auth=DispatchProxy.Create<IAuthService,Auth>();((Auth)(object)auth).Grants=grants;c.Services.AddSingleton(auth);var service=DispatchProxy.Create<IProcurementService,Api>();api=(Api)(object)service;api.Init();c.Services.AddSingleton(service);return c;}
 public class Auth:DispatchProxy{public string[] Grants=[];protected override object? Invoke(MethodInfo? m,object?[]? a)=>m!.Name==nameof(IAuthService.GetRolesAsync)?Task.FromResult<IReadOnlyCollection<string>>(["SuperAdmin"]):m.Name==nameof(IAuthService.GetPermissionsAsync)?Task.FromResult<IReadOnlyCollection<string>>(Grants):Task.CompletedTask;}
 public class Api:DispatchProxy
 {
  public Guid Id=Guid.NewGuid(),Company=Guid.NewGuid(),Site=Guid.NewGuid(),Vendor=Guid.NewGuid(),Material=Guid.NewGuid(),Project=Guid.NewGuid(),Request=Guid.NewGuid(),Code=Guid.NewGuid();public PurchaseOrderResponse Order=null!;public int Updates,Creates;public bool Fail;public string? Saved;public Guid SavedId;
  public void Init(){Order=JsonSerializer.Deserialize<PurchaseOrderResponse>(JsonSerializer.Serialize(new{Id,CompanyId=Company,ConstructionSiteId=Site,VendorId=Vendor,VendorName="Supplier",PurchaseOrderNumber="PO-006",OrderDate=new DateOnly(2026,11,2),Status="Draft",ProjectId=Project,MaterialRequestId=Request,ExpectedDeliveryDate=new DateOnly(2026,11,20),CurrencyCode="USD",ExchangeRate=2,DiscountAmount=1,DeliveryAddress="Warehouse",PaymentTerms="Net30",Notes="Original",Lines=new[]{new{Id=Guid.NewGuid(),MaterialId=Material,MaterialName="Cement",OrderedQuantity=2,UnitPrice=10,TaxAmount=3,CostCodeId=Code}}}))!;}
  protected override object? Invoke(MethodInfo? m,object?[]? a)=>m!.Name switch{
   nameof(IProcurementService.GetPurchaseOrderAsync)=>Task.FromResult<PurchaseOrderResponse?>(Order),
   nameof(IProcurementService.GetMaterialsAsync)=>Task.FromResult(new List<MaterialResponse>{new(){Id=Material,CompanyId=Company,Name="Cement",IsActive=true}}),
   nameof(IProcurementService.GetVendorsAsync)=>Task.FromResult(new List<SupplierResponse>{JsonSerializer.Deserialize<SupplierResponse>(JsonSerializer.Serialize(new{Id=Vendor,CompanyId=Company,Name="Supplier",IsActive=true}))!}),
   "UpdatePurchaseOrderAsync"=>Update(a!),nameof(IProcurementService.CreatePurchaseOrderAsync)=>Create(),_=>throw new InvalidOperationException("Unexpected procurement operation")};
  private Task Update(object?[] a){Updates++;SavedId=(Guid)a[0]!;Saved=JsonSerializer.Serialize(a[1],a[1]!.GetType(),new JsonSerializerOptions(JsonSerializerDefaults.Web));return Fail?Task.FromException(new Exception("private diagnostic")):Task.CompletedTask;}
  private Task Create(){Creates++;return Task.CompletedTask;}
 }
}
