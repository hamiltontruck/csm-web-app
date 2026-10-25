using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockTransferPermissionUiTests
{
    private static readonly Guid SiteId = Guid.NewGuid();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transfer_history_only_offers_creation_with_adjust_permission(bool canAdjust)
    {
        using var context = Context(canAdjust, out _, out _);
        var page = context.RenderComponent<StockTransfersSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        Assert.Contains("Cement", page.Markup);
        Assert.Equal(canAdjust, page.FindAll("button").Any(b => b.TextContent.Contains("New Transfer")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Direct_create_route_requires_adjust_permission_before_loading_form(bool siteRoute)
    {
        using var context = Context(false, out var sites, out var inventory);
        var page = context.RenderComponent<StockTransferCreate>(p => p.Add(x => x.SiteId, siteRoute ? SiteId : (Guid?)null));
        page.WaitForAssertion(() => Assert.Contains("You do not have permission to create stock transfers.",
            page.Find("[role=alert]").TextContent));
        Assert.Empty(page.FindAll("select, input, textarea"));
        Assert.DoesNotContain("Save Transfer", page.Markup);
        Assert.Contains("Back", page.Markup);
        Assert.Equal(0, sites.Calls);
        Assert.Equal(0, inventory.Calls);
    }

    [Fact]
    public void Inventory_operator_with_adjust_permission_can_open_create_form()
    {
        using var context = Context(true, out _, out _);
        var page = context.RenderComponent<StockTransferCreate>();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        Assert.Equal(3, page.FindAll("select").Count);
        Assert.Contains("Save Transfer", page.Markup);
    }

    private static TestContext Context(bool canAdjust, out Sites sites, out Inventory inventory)
    {
        var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        ((Auth)(object)auth).CanAdjust = canAdjust;
        var projects = DispatchProxy.Create<IProjectService, Sites>();
        sites = (Sites)(object)projects;
        var stock = DispatchProxy.Create<IInventoryService, Inventory>();
        inventory = (Inventory)(object)stock;
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(projects);
        context.Services.AddSingleton(stock);
        return context;
    }

    public class Auth : DispatchProxy
    {
        public bool CanAdjust { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IAuthService.GetPermissionsAsync)
                ? Task.FromResult<IReadOnlyCollection<string>>(CanAdjust
                    ? new[] { "Procurement_Inventory_View", "Procurement_Inventory_Adjust" }
                    : new[] { "Procurement_Inventory_View" })
                : Task.CompletedTask;
    }

    public class Sites : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            return Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
        }
    }

    public class Inventory : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            return method!.Name == nameof(IInventoryService.GetStockTransfersAsync)
                ? Task.FromResult(new List<StockTransferResponse> { new() { FromSiteId = SiteId, MaterialName = "Cement" } })
                : throw new InvalidOperationException("Unexpected inventory call.");
        }
    }
}
