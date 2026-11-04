using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.Finance;
using CSM.Web.Models.Finance;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InvoicePaymentFormTests
{
    private static readonly Guid InvoiceId = Guid.NewGuid(), VendorId = Guid.NewGuid(), SiteId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), PayrollId = Guid.NewGuid();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Invoice_payment_loads_without_vendor_catalog_and_preserves_invoice_fields(bool site, bool client)
    {
        using var context = Context(out var state);
        state.Site = site; state.Client = client;
        var page = context.RenderComponent<PaymentCreate>();
        page.WaitForAssertion(() => Assert.Contains("ACC-INV-TEST-001", page.Markup));
        Assert.Empty(page.FindAll(".alert-danger"));
        if (!client) Assert.Contains(page.FindAll("input"), x => x.GetAttribute("value") == "Test Cement Supplier");
        page.Find("input[placeholder='Enter payment number']").Change("ACC-PAY-TEST-001");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Saved));
        Assert.Equal(InvoiceId, state.Saved!.InvoiceId);
        Assert.Equal(client ? null : VendorId, state.Saved.VendorId);
        Assert.Equal(site ? SiteId : null, state.Saved.ConstructionSiteId);
        Assert.Equal(site ? ProjectId : null, state.Saved.ProjectId);
        Assert.Equal(client ? 1 : 2, state.Saved.Direction);
        Assert.Equal(950m, state.Saved.Amount);
        Assert.Equal("ETB", state.Saved.CurrencyCode);
        Assert.Equal(1.25m, state.Saved.ExchangeRate);
        Assert.Equal("Cash", state.Saved.PaymentMethod);
        Assert.Null(state.Saved.PayrollRecordId);
        var path = site ? $"http://localhost/finance/sites/{SiteId}/payments/{PaymentId}" : $"http://localhost/finance/payments/{PaymentId}";
        page.WaitForAssertion(() => Assert.Equal(path, context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_returns_to_the_registered_invoice_details_route(bool site)
    {
        using var context = Context(out var state);
        state.Site = site;
        var page = context.RenderComponent<PaymentCreate>();
        page.WaitForAssertion(() => Assert.Contains("ACC-INV-TEST-001", page.Markup));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        var path = site ? $"http://localhost/finance/sites/{SiteId}/invoices/{InvoiceId}" : $"http://localhost/finance/invoices/{InvoiceId}";
        Assert.Equal(path, context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Null(state.Saved);
    }

    [Fact]
    public void Denied_invoice_load_does_not_allow_saving_an_uninitialized_payment()
    {
        using var context = Context(out var state);
        state.FailLoad = true;
        var page = context.RenderComponent<PaymentCreate>();
        page.WaitForAssertion(() => Assert.Contains("Invoice load denied", page.Markup));
        Assert.True(page.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Null(state.Saved);
    }

    [Fact]
    public void Returning_from_payroll_to_invoice_restores_invoice_and_clears_payroll_link()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<PaymentCreate>();
        page.WaitForAssertion(() => Assert.Contains("ACC-INV-TEST-001", page.Markup));
        page.Find("select").Change("Payroll");
        page.WaitForAssertion(() => Assert.Contains("Payroll Employee", page.Markup));
        page.FindAll("select").ElementAt(1).Change(PayrollId.ToString());
        page.Find("select").Change("Invoice");
        page.WaitForAssertion(() => Assert.Contains("ACC-INV-TEST-001", page.Markup));
        page.Find("input[placeholder='Enter payment number']").Change("ACC-PAY-TEST-001");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Saved));
        Assert.Equal(InvoiceId, state.Saved!.InvoiceId);
        Assert.Equal(VendorId, state.Saved.VendorId);
        Assert.Null(state.Saved.PayrollRecordId);
        Assert.Equal(950m, state.Saved.Amount);
    }

    private static TestContext Context(out FinanceProxy state)
    {
        var context = new TestContext();
        context.Services.AddLogging();
        var service = DispatchProxy.Create<IFinanceService, FinanceProxy>();
        state = (FinanceProxy)(object)service;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, DeniedVendorProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IPayrollService, PayrollProxy>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/finance/payments/create?invoiceId={InvoiceId}");
        return context;
    }

    public class DeniedVendorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<List<CSM.Web.Models.Procurement.VendorResponse>>(new HttpRequestException("403 Forbidden"));
    }

    public class PayrollProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<PayrollResponse>>([new() { Id = PayrollId, EmployeeName = "Payroll Employee", EmployeeNumber = "EMP-001", CurrencyCode = "ETB", NetPay = 500, Status = "Approved" }]);
    }

    public class FinanceProxy : DispatchProxy
    {
        public bool Site, Client, FailLoad;
        public CreatePaymentRequest? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "GetInvoiceAsync": return FailLoad ? Task.FromException<InvoiceResponse?>(new HttpRequestException("Invoice load denied")) : Task.FromResult<InvoiceResponse?>(new()
                    { Id = InvoiceId, InvoiceNumber = "ACC-INV-TEST-001", ConstructionSiteId = Site ? SiteId : null, ProjectId = Site ? ProjectId : null,
                      VendorId = Client ? null : VendorId, InvoiceType = Client ? "ClientInvoice" : "VendorBill", TotalAmount = 1150, PaidAmount = 200, CurrencyCode = "ETB", ExchangeRate = 1.25m, Status = "Issued" });
                case "GetInvoiceVendorOptionsAsync": Assert.Equal(InvoiceId, args![0]); return Task.FromResult<IReadOnlyList<InvoiceVendorOption>>([new(VendorId, "VEN-001", "Test Cement Supplier")]);
                case "CreatePaymentAsync": Saved = (CreatePaymentRequest)args![0]!; return Task.FromResult(PaymentId);
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
