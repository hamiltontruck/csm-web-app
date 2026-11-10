using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class MaterialCatalogueRowAccessTests
{
    private static readonly Guid MaterialId = Guid.Parse("386ca108-6c04-42a2-b91e-7badcf118089");

    [Theory]
    [InlineData("ProcurementOfficer", true, false)]
    [InlineData("Accountant", false, false)]
    [InlineData("SiteManager", false, false)]
    [InlineData("SiteManager", true, true)]
    public void Catalogue_row_opens_edit_only_for_an_authorized_catalogue_manager(string role, bool adjust, bool canEdit)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, [role], adjust
            ? ["Procurement_Inventory_View", "Procurement_Inventory_Adjust"]
            : ["Procurement_Inventory_View"]);
        context.Services.AddSingleton(DispatchProxy.Create<IProcurementService, Catalogue>());
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/materials");
        var page = context.RenderComponent<Materials>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Contains("Cement", page.Find("tbody").TextContent);
        Assert.Equal("Active", page.Find("tbody .badge").TextContent.Trim());
        Assert.Equal(canEdit, page.FindAll("button").Any(b => b.TextContent.Contains("New Material")));
        page.Find("tbody tr").Click();
        Assert.Equal(canEdit ? $"http://localhost/materials/{MaterialId}/edit" : "http://localhost/materials", navigation.Uri);
    }

    public class Catalogue : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IProcurementService.GetMaterialsAsync)) throw new InvalidOperationException(method.Name);
            return Task.FromResult(new List<MaterialResponse> { new() {
                Id = MaterialId, MaterialCode = "MAT-001", Name = "Cement", Category = "Cement", UnitOfMeasure = "Bag", IsActive = true
            } });
        }
    }
}
