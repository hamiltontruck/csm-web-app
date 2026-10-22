using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockSiteFeedbackTests
{
    private static readonly Guid SiteId = Guid.NewGuid();

    [Fact]
    public void Site_outside_accessible_list_shows_access_message_without_requesting_stock()
    {
        using var context = Context(false, out var inventory);
        var page = context.RenderComponent<StockBalanceSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("unavailable or you do not have permission",
            page.Find("[role=alert]").TextContent));
        Assert.Equal(0, inventory.Calls);
        Assert.DoesNotContain("No stock available", page.Markup);
        Assert.DoesNotContain("Hamilton Test Site", page.Markup);
        Assert.Empty(page.FindAll("#retry-stock-balances"));
    }

    [Fact]
    public void Accessible_site_with_no_stock_keeps_genuine_empty_state()
    {
        using var context = Context(true, out var inventory);
        var page = context.RenderComponent<StockBalanceSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("No stock available for this site.", page.Markup));
        Assert.Contains("Hamilton Test Site", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Equal(1, inventory.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Please sign in again.")]
    [InlineData(HttpStatusCode.Forbidden, "You do not have permission")]
    public void Site_lookup_distinguishes_session_and_access_failure(HttpStatusCode status, string message)
    {
        using var context = Context(true, out var inventory);
        ((Sites)(object)context.Services.GetRequiredService<IProjectService>()).Status = status;
        var page = context.RenderComponent<StockBalanceSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains(message, page.Find("[role=alert]").TextContent));
        Assert.Equal(0, inventory.Calls);
        Assert.DoesNotContain("No stock available", page.Markup);
        Assert.Empty(page.FindAll("#retry-stock-balances"));
        if (status == HttpStatusCode.Unauthorized)
            Assert.Equal("/login", page.Find("#stock-site-sign-in").GetAttribute("href"));
    }

    [Fact]
    public void Changing_site_URL_clears_previous_site_data_and_checks_new_access()
    {
        using var context = Context(true, out var inventory);
        var page = context.RenderComponent<StockBalanceSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        page.SetParametersAndRender(p => p.Add(x => x.SiteId, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Contains("unavailable or you do not have permission",
            page.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("Hamilton Test Site", page.Markup);
        Assert.Equal(1, inventory.Calls);
    }

    private static TestContext Context(bool allowed, out Stock inventory)
    {
        var context = new TestContext();
        var sites = DispatchProxy.Create<IProjectService, Sites>();
        ((Sites)(object)sites).Allowed = allowed;
        var stock = DispatchProxy.Create<IInventoryService, Stock>();
        inventory = (Stock)(object)stock;
        context.Services.AddSingleton(sites);
        context.Services.AddSingleton(stock);
        return context;
    }

    public class Sites : DispatchProxy
    {
        public bool Allowed { get; set; }
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Status.HasValue ?
            Task.FromException<List<SiteResponse>>(new HttpRequestException("private diagnostic", null, Status)) :
            Task.FromResult(Allowed ? new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } } : []);
    }

    public class Stock : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            return Task.FromResult(new List<CSM.Web.Models.Procurement.StockBalanceResponse>());
        }
    }
}
