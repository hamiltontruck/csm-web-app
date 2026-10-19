using System.Reflection;
using AngleSharp.Html.Dom;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockAdjustmentTypeTests
{
    [Theory]
    [InlineData("Increase", true)]
    [InlineData("Decrease", false)]
    public void Visible_adjustment_type_matches_saved_direction(string selected, bool expected)
    {
        using var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        var page = context.RenderComponent<StockAdjustmentCreate>();

        var initial = (IHtmlSelectElement)page.FindAll("select").ToArray()[2];
        Assert.Equal("Increase", initial.GetAttribute("value"));
        page.FindAll("select").ToArray()[0].Change(SiteId.ToString());
        page.FindAll("select").ToArray()[1].Change(MaterialId.ToString());
        page.FindAll("select").ToArray()[2].Change("Decrease");
        page.FindAll("select").ToArray()[2].Change(selected);
        var type = (IHtmlSelectElement)page.FindAll("select").ToArray()[2];
        Assert.Equal(selected, type.GetAttribute("value"));

        // Other fields trigger re-rendering; the chosen type must remain visible.
        page.FindAll("input").ToArray()[1].Change("2");
        page.FindAll("input").ToArray()[3].Change("ADJ-CORR-001");
        page.FindAll("select").ToArray()[3].Change("Count Correction");
        Assert.Equal(selected, page.FindAll("select").ToArray()[2].GetAttribute("value"));
        page.Find("button.btn-primary").Click();

        var request = Assert.Single(((InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>()).Requests);
        Assert.Equal(expected, request.Increase);
        Assert.Equal(2m, request.Quantity);
        Assert.Equal(SiteId, request.ConstructionSiteId);
        Assert.Equal(MaterialId, request.MaterialId);
    }

    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();

    public class ProjectProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }

    public class InventoryProxy : DispatchProxy
    {
        public List<StockAdjustmentCreateRequest> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetStockBySiteAsync")
                return Task.FromResult(new List<StockBalanceResponse> {
                    new() { MaterialId = MaterialId, MaterialName = "Cement", QuantityOnHand = 6m } });
            if (method.Name == "CreateStockAdjustmentAsync")
            {
                Requests.Add((StockAdjustmentCreateRequest)args![0]!);
                return Task.FromResult(true);
            }
            throw new NotSupportedException(method.Name);
        }
    }
}
