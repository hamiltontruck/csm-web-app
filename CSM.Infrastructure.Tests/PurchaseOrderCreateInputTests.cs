using System.Reflection;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApiRequest = CSM.Application.Procurement.PurchaseOrders.Dtos.CreatePurchaseOrderRequest;

namespace CSM.Infrastructure.Tests;

public sealed class PurchaseOrderCreateInputTests
{
    private static readonly Guid Site = Guid.NewGuid(), Material = Guid.NewGuid(), Vendor = Guid.NewGuid();
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("price", true)] [InlineData("price", false)]
    [InlineData("quantity", true)] [InlineData("quantity", false)]
    [InlineData("header", true)] [InlineData("header", false)]
    [InlineData("number", true)] [InlineData("number", false)]
    public void Save_sends_latest_form_values_using_the_API_contract(string field, bool inputOnly)
    {
        using var context = Context(out var state);
        var page = Render(context);
        Fill(page);
        var (label, index, value) = field switch {
            "price" => ("Unit Price (ETB)", 0, "100.25"),
            "quantity" => ("Quantity", 0, "2.5"),
            "header" => ("Remarks", 0, "Procurement Officer purchase order test."),
            _ => ("PO Number", 0, "PO-INPUT-TEST-002")
        };
        Enter(Field(page, label, index), value, inputOnly);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        var request = JsonSerializer.Deserialize<ApiRequest>(state.Payload!, Wire)!;
        Assert.Equal(Site, request.ConstructionSiteId);
        Assert.Equal(Vendor, request.VendorId);
        Assert.Equal(Material, Assert.Single(request.Lines).MaterialId);
        switch (field) {
            case "price": Assert.Equal(100.25m, request.Lines[0].UnitPrice); break;
            case "quantity": Assert.Equal(2.5m, request.Lines[0].OrderedQuantity); break;
            case "header": Assert.Equal(value, request.Notes); break;
            case "number": Assert.Equal(value, request.PurchaseOrderNumber); break;
        }
    }

    [Theory]
    [InlineData("Quantity", "")] [InlineData("Unit Price (ETB)", "")]
    [InlineData("Quantity", "79228162514264337593543950336")]
    [InlineData("Unit Price (ETB)", "79228162514264337593543950336")]
    public void Invalid_number_blocks_save_until_corrected_instead_of_reusing_old_value(string field, string invalid)
    {
        using var context = Context(out var state);
        var page = Render(context);
        Fill(page);
        Enter(Field(page, field), invalid, true);
        page.Find("form").Submit();
        Assert.Equal(0, state.SaveCalls);
        var displayName = field == "Quantity" ? "Quantity" : "Unit Price";
        Assert.Contains(page.FindAll(".validation-errors li"), message =>
            message.TextContent.Contains(displayName, StringComparison.Ordinal) &&
            message.TextContent.Contains("number", StringComparison.OrdinalIgnoreCase));
        Enter(Field(page, field), "25.50", true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        var request = JsonSerializer.Deserialize<ApiRequest>(state.Payload!, Wire)!;
        Assert.Equal(25.50m, field == "Quantity" ? request.Lines[0].OrderedQuantity : request.Lines[0].UnitPrice);
        Assert.Empty(page.FindAll(".validation-errors li"));
    }

    [Fact]
    public void Latest_line_values_remain_attached_to_the_surviving_row()
    {
        using var context = Context(out var state);
        var page = Render(context);
        Fill(page);
        page.FindAll("button").Single(x => x.TextContent.Contains("Add Item")).Click();
        Field(page, "Material", 1).Change(Material.ToString());
        Enter(Field(page, "Quantity", 1), "3", true);
        Enter(Field(page, "Unit Price (ETB)", 1), "75.25", true);
        page.FindAll("button").First(x => x.TextContent.Trim() == "Remove").Click();
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        var request = JsonSerializer.Deserialize<ApiRequest>(state.Payload!, Wire)!;
        var line = Assert.Single(request.Lines);
        Assert.Equal(3m, line.OrderedQuantity);
        Assert.Equal(75.25m, line.UnitPrice);
    }

    private static void Enter(IElement field, string value, bool inputOnly)
    {
        if (!inputOnly) { field.Change(value); return; }
        // A real browser can type even without an input listener. Submit anyway
        // so RED asserts the wrong saved value rather than a bUnit event error.
        try { field.Input(value); }
        catch (Exception ex) when (ex.GetType().Name == "MissingEventHandlerException") { }
    }
    private static IElement Field(IRenderedFragment page, string label, int index = 0) => page.FindAll("label")
        .Where(x => x.TextContent.Trim() == label).ElementAt(index).ParentElement!.QuerySelector("input,select,textarea")!;
    private static void Fill(IRenderedFragment page)
    {
        Field(page, "PO Number").Change("PO-INPUT-TEST-001");
        Field(page, "Order Date").Change("2026-11-08");
        Field(page, "Supplier").Change(Vendor.ToString());
        Field(page, "Material").Change(Material.ToString());
        Field(page, "Quantity").Change("1");
        Field(page, "Unit Price (ETB)").Change("12.5");
        Field(page, "Remarks", 0).Change("Initial header");
    }
    private static IRenderedComponent<PurchaseOrderCreate> Render(TestContext context)
    {
        var page = context.RenderComponent<PurchaseOrderCreate>(p => p.Add(x => x.SiteId, Site));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        return page;
    }
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new();
        var api = DispatchProxy.Create<IProcurementService, Api>();
        ((Api)(object)api).State = state;
        context.Services.AddSingleton(api);
        return context;
    }
    public sealed class State { public int SaveCalls; public string? Payload; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IProcurementService.GetMaterialsAsync) => Task.FromResult(new List<MaterialResponse> { new() { Id = Material, Name = "Cement", UnitOfMeasure = "Bag" } }),
            nameof(IProcurementService.GetVendorsAsync) => Task.FromResult(new List<SupplierResponse> { new() { Id = Vendor, Name = "Supplier" } }),
            nameof(IProcurementService.CreatePurchaseOrderAsync) => Save(args![0]!),
            _ => throw new NotSupportedException(method.Name)
        };
        private Task Save(object request) { State.SaveCalls++; State.Payload = JsonSerializer.Serialize(request, request.GetType(), Wire); return Task.CompletedTask; }
    }
}
