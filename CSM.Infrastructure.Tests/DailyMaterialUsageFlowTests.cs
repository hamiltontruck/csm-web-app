using System.Reflection;
using Bunit;
using CSM.Domain.Enums;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Projects;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class DailyMaterialUsageFlowTests
{
    [Fact]
    public void Empty_logs_have_explicit_creation_and_select_the_confirmed_log()
    {
        using var c = Context(out var api); var p = Page(c, api);
        Field(p, "Project").Change(api.Project.ToString());
        p.WaitForAssertion(() => Assert.Contains("No daily progress logs", p.Markup));
        p.Find("#create-usage-daily-log").Click();
        p.WaitForAssertion(() => Assert.NotNull(api.CreatedLog));
        Assert.Equal(api.Project, api.CreatedLog!.ProjectId); Assert.Equal(Guid.Empty, api.CreatedLog.TaskId);
        Assert.Equal(api.Log.ToString(), Field(p, "Daily Progress Log").GetAttribute("value"));
        Assert.Equal(0, api.Saves);
    }
    [Fact]
    public void Existing_logs_allow_explicit_daily_log_for_another_date()
    {
        using var c=Context(out var api);api.HasLog=true;var p=Page(c,api);Field(p,"Project").Change(api.Project.ToString());
        p.Find("#create-usage-daily-log");
        Field(p,"Daily Log Date").Change("2026-11-13");p.Find("#create-usage-daily-log").Click();
        p.WaitForAssertion(()=>Assert.NotNull(api.CreatedLog));Assert.Equal(new DateOnly(2026,11,13),api.CreatedLog!.LogDate);
        Assert.Equal("2026-11-13",Field(p,"Usage Date").GetAttribute("value"));Assert.Equal(0,api.Saves);
    }
    [Fact]
    public void Route_site_is_visibly_selected_and_parent_change_clears_old_log()
    {
        using var c = Context(out var api); api.HasLog = true; var p = Page(c, api);
        Assert.Equal(api.Site.ToString(), Field(p, "Construction Site").GetAttribute("value"));
        Select(p, api); Field(p,"Quantity Issued").Change("200"); Field(p, "Project").Change(api.OtherProject.ToString());
        p.FindAll("button").First(x=>x.TextContent.Trim()=="Save Usage").Click(); Assert.Equal(0, api.Saves);
        Assert.NotEqual(api.Log.ToString(), Field(p, "Daily Progress Log").GetAttribute("value"));
    }
    [Fact]
    public void Failed_save_retains_quantities_and_recovers_without_a_new_log()
    {
        using var c = Context(out var api); api.HasLog = true; var p = Page(c, api); Select(p, api);
        Field(p,"Quantity Issued").Change("200"); Field(p,"Quantity Used").Change("100"); Field(p,"Quantity Returned").Change("50"); Field(p,"Notes").Change("Usage note");
        api.SaveFailure = true; p.FindAll("button").First(x=>x.TextContent.Trim()=="Save Usage").Click();
        p.WaitForAssertion(() => Assert.Contains("Unable to save",p.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("private diagnostic",p.Markup); Assert.Equal("200",Field(p,"Quantity Issued").GetAttribute("value"));
        api.SaveFailure = false; p.FindAll("button").First(x=>x.TextContent.Trim()=="Save Usage").Click(); p.WaitForAssertion(() => Assert.Equal(2,api.Saves));
        Assert.Equal(200,api.Saved!.QuantityIssued); Assert.Equal(100,api.Saved.QuantityUsed); Assert.Equal(50,api.Saved.QuantityReturned); Assert.Equal(api.Log,api.Saved.DailyProgressLogId);
        Assert.Null(api.CreatedLog); Assert.EndsWith($"/daily-material-usage/site/{api.Site}",c.Services.GetRequiredService<NavigationManager>().Uri);
    }
    [Fact]
    public void Log_load_failure_is_retryable_and_does_not_offer_creation_as_if_empty()
    {
        using var c = Context(out var api); var p = Page(c,api); api.LogFailure=true;
        Field(p,"Project").Change(api.Project.ToString());
        p.WaitForAssertion(()=>Assert.Single(p.FindAll("[role=alert]"))); Assert.Empty(p.FindAll("#create-usage-daily-log"));
        api.LogFailure=false; api.HasLog=true; p.Find("#reload-usage-daily-logs").Click();
        p.WaitForAssertion(()=>Assert.Contains(api.Log.ToString(),p.Markup)); Assert.Equal(0,api.Saves);
    }
    [Fact]
    public void Missing_daily_progress_create_grant_hides_log_creation()
    {
        using var c=Context(out var api,false);var p=Page(c,api);Field(p,"Project").Change(api.Project.ToString());
        p.WaitForAssertion(()=>Assert.Contains("No daily progress logs",p.Markup));Assert.Empty(p.FindAll("#create-usage-daily-log"));
    }
    [Fact]
    public void Site_history_failure_can_retry_without_showing_empty_history()
    {
        using var c=Context(out var api);api.HistoryFailure=true;var p=c.RenderComponent<DailyMaterialUsageSite>(x=>x.Add(y=>y.SiteId,api.Site));
        p.WaitForAssertion(()=>Assert.Single(p.FindAll("[role=alert]")));Assert.DoesNotContain("No daily usage",p.Markup);
        api.HistoryFailure=false;p.Find("#retry-daily-usage-history").Click();p.WaitForAssertion(()=>Assert.Contains("Cement",p.Markup));Assert.DoesNotContain("Other site",p.Markup);
    }
    private static void Select(IRenderedFragment p,Api api){Field(p,"Project").Change(api.Project.ToString());Field(p,"Daily Progress Log").Change(api.Log.ToString());Field(p,"Material").Change(api.Material.ToString());}
    private static AngleSharp.Dom.IElement Field(IRenderedFragment p,string label)=>p.FindAll("label").First(x=>x.TextContent.Trim()==label).ParentElement!.QuerySelector("input,select,textarea")!;
    private static IRenderedComponent<DailyMaterialUsageCreate> Page(TestContext c,Api api){var p=c.RenderComponent<DailyMaterialUsageCreate>(x=>x.Add(y=>y.SiteId,api.Site));p.WaitForAssertion(()=>Assert.Contains("MORMOR",p.Markup));return p;}
    private static TestContext Context(out Api api,bool create=true)
    {
        var c=new TestContext();WorkspaceTestAccess.Add(c,["SuperAdmin"],Enum.GetNames<GranularPermission>().Where(x=>create||x!="Projects_DailyProgress_Create").ToArray());
        var project=DispatchProxy.Create<IProjectService,Api>();api=(Api)(object)project;c.Services.AddSingleton(project);
        var inventory=DispatchProxy.Create<IInventoryService,Inventory>();((Inventory)(object)inventory).Api=api;c.Services.AddSingleton(inventory);return c;
    }
    public class Api:DispatchProxy
    {
        public Guid Site=Guid.NewGuid(),Project=Guid.NewGuid(),OtherProject=Guid.NewGuid(),Log=Guid.NewGuid(),Material=Guid.NewGuid();
        public bool HasLog,LogFailure,SaveFailure,HistoryFailure;public int Saves;public CreateDailyMaterialUsageRequest? Saved;public CreateDailyProgressRequest? CreatedLog;
        protected override object? Invoke(MethodInfo? m,object?[]? a)=>m!.Name switch{
            nameof(IProjectService.GetSitesAsync)=>Task.FromResult(new List<SiteResponse>{new(){Id=Site,Name="MORMOR",SiteCode="SITE-002",IsActive=true}}),
            nameof(IProjectService.GetProjectsBySiteAsync)=>Task.FromResult(new List<ProjectResponse>{new(){Id=Project,ConstructionSiteId=Site,Name="Mor Fence",ProjectCode="FENCE"},new(){Id=OtherProject,ConstructionSiteId=Site,Name="Other project"}}),
            nameof(IProjectService.GetDailyProgressLogsAsync)=>LogFailure?Task.FromException<List<DailyProgressLogResponse>>(new HttpRequestException("private diagnostic",null,System.Net.HttpStatusCode.ServiceUnavailable)):Task.FromResult(HasLog&&(Guid)a![0]! == Project?new List<DailyProgressLogResponse>{new(){Id=Log,ProjectId=Project,LogDate=CreatedLog?.LogDate.ToDateTime(TimeOnly.MinValue) ?? new DateTime(2026,11,14)}}:new()),
            nameof(IProjectService.GetOrCreateDailyLogAsync)=>CreateLog((CreateDailyProgressRequest)a![0]!),_=>throw new InvalidOperationException("Unexpected project call "+m.Name)};
        private Task<Guid> CreateLog(CreateDailyProgressRequest request){CreatedLog=request;HasLog=true;return Task.FromResult(Log);}
        public Task<bool> Save(CreateDailyMaterialUsageRequest request){Saves++;Saved=request;return SaveFailure?Task.FromException<bool>(new Exception("private diagnostic")):Task.FromResult(true);}
    }
    public class Inventory:DispatchProxy
    {
        public Api Api=null!;protected override object? Invoke(MethodInfo? m,object?[]? a)=>m!.Name switch{
            nameof(IInventoryService.GetStockBySiteAsync)=>Task.FromResult(new List<StockBalanceResponse>{new(){MaterialId=Api.Material,MaterialCode="MAT-001",MaterialName="Cement",QuantityOnHand=700,UnitOfMeasure="Bag"}}),
            nameof(IInventoryService.CreateDailyMaterialUsageAsync)=>Api.Save((CreateDailyMaterialUsageRequest)a![0]!),
            nameof(IInventoryService.GetDailyMaterialUsageAsync)=>Api.HistoryFailure?Task.FromException<List<DailyMaterialUsageResponse>>(new HttpRequestException("private diagnostic",null,System.Net.HttpStatusCode.ServiceUnavailable)):Task.FromResult(new List<DailyMaterialUsageResponse>{new(){ConstructionSiteId=Api.Site,MaterialName="Cement"},new(){ConstructionSiteId=Guid.NewGuid(),MaterialName="Other site"}}),_=>throw new InvalidOperationException("Unexpected inventory call "+m.Name)};
    }
}
