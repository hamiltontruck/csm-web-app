using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages.Finance;
using CSM.Web.Models.Finance;
using CSM.Web.Models.Projects;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InvoiceEditPreservationTests
{
    private static readonly Guid InvoiceId = Guid.NewGuid(), SiteId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), VendorId = Guid.NewGuid();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Editing_due_date_preserves_API_financial_fields_and_hidden_metadata(bool site, bool client)
    {
        using var context = Context(out var state);
        state.Site = site; state.Client = client;
        // Use the generic route even for a site invoice: persisted scope must be retained.
        var page = context.RenderComponent<InvoiceEdit>(p => p.Add(x => x.Id, InvoiceId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Equal(2.5m, decimal.Parse(page.FindAll("input[type=number]").ElementAt(0).GetAttribute("value")!));
        Assert.Equal(1000m, decimal.Parse(page.FindAll("input[type=number]").ElementAt(1).GetAttribute("value")!));
        Assert.Equal(150m, decimal.Parse(page.FindAll("input[type=number]").ElementAt(2).GetAttribute("value")!));
        Assert.Equal(client ? "1" : "2", page.FindAll("select").ElementAt(2).GetAttribute("value"));
        page.FindAll("input[type=date]").ElementAt(1).Change("2026-12-05");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Saved));
        var request = JsonSerializer.SerializeToElement(state.Saved);
        Assert.Equal(1000m, request.GetProperty("Subtotal").GetDecimal());
        Assert.Equal(150m, request.GetProperty("TaxAmount").GetDecimal());
        Assert.Equal(1150m, request.GetProperty("TotalAmount").GetDecimal());
        Assert.Equal(2.5m, request.GetProperty("ExchangeRate").GetDecimal());
        Assert.Equal("USD", request.GetProperty("CurrencyCode").GetString());
        Assert.Equal(client ? 1 : 2, request.GetProperty("Type").GetInt32());
        Assert.Equal("Accountant invoice test.", request.GetProperty("Description").GetString());
        Assert.Equal("ACC-INV-REF-001", request.GetProperty("ExternalReference").GetString());
        Assert.Equal("2026-12-05", request.GetProperty("DueDate").GetString());
        Assert.Equal(site ? SiteId : (Guid?)null, request.GetProperty("ConstructionSiteId").ValueKind == JsonValueKind.Null ? null : request.GetProperty("ConstructionSiteId").GetGuid());
        Assert.Equal(site ? ProjectId : (Guid?)null, state.Saved!.ProjectId);
        var expected = site ? $"http://localhost/finance/sites/{SiteId}/invoices/{InvoiceId}" : $"http://localhost/finance/invoices/{InvoiceId}";
        page.WaitForAssertion(() => Assert.Equal(expected, context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Fact]
    public void Failed_load_never_exposes_a_savable_empty_invoice()
    {
        using var context = Context(out var state);
        state.FailLoad = true;
        var page = context.RenderComponent<InvoiceEdit>(p => p.Add(x => x.Id, InvoiceId));
        page.WaitForAssertion(() => Assert.Contains("Invoice load denied", page.Markup));
        Assert.Empty(page.FindAll("form"));
        Assert.Null(state.Saved);
    }

    [Fact]
    public void Changing_record_parameters_reloads_values_and_does_not_reuse_the_previous_invoice()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<InvoiceEdit>(p => p.Add(x => x.Id, InvoiceId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        state.Subtotal = 2000m;
        var second = Guid.NewGuid();
        page.SetParametersAndRender(p => p.Add(x => x.Id, second));
        page.WaitForAssertion(() => Assert.Equal(2000m, decimal.Parse(page.FindAll("input[type=number]").ElementAt(1).GetAttribute("value")!)));
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(second, state.SavedId));
        Assert.Equal(2000m, state.Saved!.Subtotal);
        Assert.Equal(150m, state.Saved.TaxAmount);
    }

    private static TestContext Context(out FinanceProxy state)
    {
        var context = new TestContext();
        context.Services.AddLogging();
        var finance = DispatchProxy.Create<IFinanceService, FinanceProxy>();
        state = (FinanceProxy)(object)finance;
        context.Services.AddSingleton(finance);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectsProxy>());
        return context;
    }

    public class ProjectsProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<ProjectResponse> { new() { Id = ProjectId, Name = "Invoice project" } });
    }

    public class FinanceProxy : DispatchProxy
    {
        public bool Site, Client, FailLoad;
        public decimal Subtotal = 1000m;
        public UpdateInvoiceRequest? Saved;
        public Guid SavedId;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "GetInvoiceAsync":
                    if (FailLoad) return Task.FromException<InvoiceResponse?>(new HttpRequestException("Invoice load denied"));
                    var body = JsonSerializer.Serialize(new { Id = (Guid)args![0]!, ConstructionSiteId = Site ? SiteId : (Guid?)null, ProjectId = Site ? ProjectId : (Guid?)null,
                        VendorId = Client ? (Guid?)null : VendorId, InvoiceNumber = "ACC-INV-TEST-001", Type = Client ? "ClientInvoice" : "VendorBill", InvoiceDate = "2026-11-04", DueDate = "2026-12-04",
                        Subtotal, TaxAmount = 150m, TotalAmount = Subtotal + 150m, CurrencyCode = "USD", ExchangeRate = 2.5m, Description = "Accountant invoice test.", ExternalReference = "ACC-INV-REF-001", Status = "Draft", Payments = Array.Empty<object>() });
                    return Task.FromResult(JsonSerializer.Deserialize<InvoiceResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                case "GetInvoiceVendorOptionsAsync": return Task.FromResult<IReadOnlyList<InvoiceVendorOption>>([new(VendorId, "VEN-001", "Test Cement Supplier")]);
                case "UpdateInvoiceAsync": SavedId = (Guid)args![0]!; Saved = (UpdateInvoiceRequest)args[1]!; return Task.CompletedTask;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
