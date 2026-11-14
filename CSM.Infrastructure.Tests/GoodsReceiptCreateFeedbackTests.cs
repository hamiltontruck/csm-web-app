using System.Reflection;
using System.Net;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class GoodsReceiptCreateFeedbackTests
{
 [Theory]
 [InlineData(401)] [InlineData(403)] [InlineData(400)] [InlineData(500)] [InlineData(0)]
 public void Failed_save_keeps_receipt_date_PO_quantity_and_remarks_without_crashing(int status)
 {
  using var context=Context(out var api);var page=context.RenderComponent<GoodsReceiptCreate>(p=>p.Add(x=>x.SiteId,api.Site));page.WaitForAssertion(()=>Assert.Single(page.FindAll("select option[value='"+api.Order+"']")));
  page.Find("input").Change("GR-005");page.Find("select").Change(api.Order.ToString());page.Find("input[type=number]").Change("2");page.Find("textarea").Change("Receipt test");
  api.Error=status==0?new Exception("private diagnostic"):new HttpRequestException("private diagnostic",null,(HttpStatusCode)status);page.Find("form").Submit();
  page.WaitForAssertion(()=>Assert.Single(page.FindAll("[role=alert]")));Assert.Single(page.FindAll("form"));Assert.Equal("GR-005",page.Find("input").GetAttribute("value"));Assert.Equal("2",page.Find("input[type=number]").GetAttribute("value"));Assert.DoesNotContain("private diagnostic",page.Markup);
  api.Error=null;page.Find("form").Submit();page.WaitForAssertion(()=>Assert.Equal(2,api.Writes));
 }
 [Theory] [InlineData(true)] [InlineData(false)]
 public void Load_or_PO_detail_errors_are_local_and_retryable(bool initial)
 {
  using var context=Context(out var api);if(initial)api.LoadFails=true;
  var page=context.RenderComponent<GoodsReceiptCreate>(p=>p.Add(x=>x.SiteId,api.Site));
  if(!initial){page.WaitForAssertion(()=>Assert.Single(page.FindAll("select")));api.DetailFails=true;page.Find("select").Change(api.Order.ToString());}
  page.WaitForAssertion(()=>Assert.Single(page.FindAll("[role=alert]")));Assert.DoesNotContain("private diagnostic",page.Markup);
 }
 [Fact]
 public void Missing_quantity_validation_keeps_form_for_correction()
 {
  using var context=Context(out var api);var page=context.RenderComponent<GoodsReceiptCreate>(p=>p.Add(x=>x.SiteId,api.Site));page.WaitForAssertion(()=>Assert.Single(page.FindAll("select")));
  page.Find("input").Change("GR-005");page.Find("select").Change(api.Order.ToString());page.Find("form").Submit();Assert.Equal(0,api.Writes);Assert.Single(page.FindAll("form"));Assert.Contains("received quantity",page.Find("[role=alert]").TextContent);
  page.Find("input[type=number]").Change("2");page.Find("form").Submit();page.WaitForAssertion(()=>Assert.Equal(1,api.Writes));
 }
 [Fact]
 public async Task Pending_save_blocks_a_second_submission()
 {
  using var context=Context(out var api);api.SaveGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var page=context.RenderComponent<GoodsReceiptCreate>(p=>p.Add(x=>x.SiteId,api.Site));page.WaitForAssertion(()=>Assert.Single(page.FindAll("select")));
  page.Find("select").Change(api.Order.ToString());page.Find("input[type=number]").Change("2");var first=page.Find("form").SubmitAsync();page.WaitForAssertion(()=>Assert.Equal(1,api.Writes));await page.Find("form").SubmitAsync();Assert.Equal(1,api.Writes);api.SaveGate.SetResult();await first;
 }
 [Fact]
 public async Task Late_old_purchase_order_response_cannot_replace_new_selection_lines()
 {
  using var context=Context(out var api);api.PendingA=new(TaskCreationOptions.RunContinuationsAsynchronously);api.PendingB=new(TaskCreationOptions.RunContinuationsAsynchronously);
  var page=context.RenderComponent<GoodsReceiptCreate>(p=>p.Add(x=>x.SiteId,api.Site));page.WaitForAssertion(()=>Assert.Single(page.FindAll("select")));
  var first=page.Find("select").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs{Value=api.Order.ToString()});
  var second=page.Find("select").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs{Value=api.Second.ToString()});
  api.PendingB.SetResult(api.PO(api.Second));await second;api.PendingA.SetResult(api.PO());await first;
  page.Find("input[type=number]").Change("2");page.Find("form").Submit();page.WaitForAssertion(()=>Assert.Equal(1,api.Writes));Assert.Equal(api.Second,api.Posted!.PurchaseOrderId);Assert.Equal(api.SecondLine,Assert.Single(api.Posted.Lines).PurchaseOrderLineId);
 }
 private static TestContext Context(out Api api){var context=new TestContext();var service=DispatchProxy.Create<IProcurementService,Api>();api=(Api)(object)service;context.Services.AddSingleton(service);return context;}
 public class Api:DispatchProxy
 {
  public Guid Site=Guid.NewGuid(),Order=Guid.NewGuid(),Line=Guid.NewGuid();public Exception? Error;public bool LoadFails,DetailFails;public int Writes;public Guid Second=Guid.NewGuid(),SecondLine=Guid.NewGuid();public CreateGoodsReceiptRequest? Posted;
  public TaskCompletionSource? SaveGate;public TaskCompletionSource<PurchaseOrderResponse?>? PendingA,PendingB;
  protected override object? Invoke(MethodInfo? method,object?[]? args)=>method!.Name switch{
   nameof(IProcurementService.GetPurchaseOrdersAsync) or nameof(IProcurementService.GetReceivablePurchaseOrdersAsync)=>LoadFails?Task.FromException<List<PurchaseOrderResponse>>(new HttpRequestException("private diagnostic")):Task.FromResult(new List<PurchaseOrderResponse>{PO()}),
   nameof(IProcurementService.GetPurchaseOrderAsync)=>DetailFails?Task.FromException<PurchaseOrderResponse?>(new HttpRequestException("private diagnostic")):(PendingA is not null ? ((Guid)args![0]! == Second ? PendingB!.Task : PendingA.Task) : Task.FromResult<PurchaseOrderResponse?>(PO())),
   nameof(IProcurementService.CreateGoodsReceiptAsync)=>Save((CreateGoodsReceiptRequest)args![0]!),_=>throw new InvalidOperationException("Unexpected API call")};
  private Task Save(CreateGoodsReceiptRequest request){Writes++;Posted=request;return Error is not null?Task.FromException(Error):SaveGate?.Task??Task.CompletedTask;}
  public PurchaseOrderResponse PO(Guid? id=null)=>new(){Id=id??Order,ConstructionSiteId=Site,PurchaseOrderNumber="PO-006",Status="SentToVendor",Lines=[new(){Id=id==Second?SecondLine:Line,MaterialId=Guid.NewGuid(),MaterialName="Cement",OrderedQuantity=10}]};
 }
}
