using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages.Finance;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class PaymentNotesInputTests
{
    private static readonly Guid InvoiceId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), VendorId = Guid.NewGuid();

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Save_uses_current_notes_with_input_only_or_blur_events(bool edit, bool inputOnly)
    {
        using var context = new TestContext();
        context.Services.AddLogging();
        var finance = DispatchProxy.Create<IFinanceService, FinanceProxy>();
        var state = (FinanceProxy)(object)finance;
        context.Services.AddSingleton(finance);
        context.Services.AddSingleton(DispatchProxy.Create<IPayrollService, UnusedPayrollProxy>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/finance/payments/create?invoiceId={InvoiceId}");
        IRenderedFragment page = edit
            ? context.RenderComponent<PaymentEdit>(p => p.Add(x => x.Id, PaymentId))
            : context.RenderComponent<PaymentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        if (!edit) page.Find("input[placeholder='Enter payment number']").Change("ACC-PAY-TEST-001");
        const string notes = "Accountant invoice payment test.";
        if (inputOnly)
        {
            // Browsers permit input events with no listener. bUnit reports that case;
            // still submit so the assertion checks the actual payment payload.
            try { page.Find("textarea").Input(notes); }
            catch (Exception ex) when (ex.GetType().Name == "MissingEventHandlerException") { }
        }
        else page.Find("textarea").Change(notes);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Saved));
        var payload = JsonSerializer.SerializeToElement(state.Saved, state.Saved!.GetType());
        Assert.Equal(notes, payload.GetProperty("Notes").GetString());
        Assert.Equal(1150m, payload.GetProperty("Amount").GetDecimal());
        Assert.Equal("ETB", payload.GetProperty("CurrencyCode").GetString());
        Assert.Equal(1m, payload.GetProperty("ExchangeRate").GetDecimal());
        Assert.Equal(InvoiceId, payload.GetProperty("InvoiceId").GetGuid());
        Assert.Equal(VendorId, payload.GetProperty("VendorId").GetGuid());
        Assert.Equal(2, payload.GetProperty("Direction").GetInt32());
    }

    public class UnusedPayrollProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new NotSupportedException(method?.Name);
    }

    public class FinanceProxy : DispatchProxy
    {
        public object? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "GetInvoiceAsync": return Task.FromResult<InvoiceResponse?>(new() { Id = InvoiceId, InvoiceNumber = "ACC-INV-TEST-001", VendorId = VendorId, InvoiceType = "VendorBill", TotalAmount = 1150, CurrencyCode = "ETB", ExchangeRate = 1, Status = "Issued" });
                case "GetInvoiceVendorOptionsAsync": return Task.FromResult<IReadOnlyList<InvoiceVendorOption>>([new(VendorId, "VEN-001", "Test Cement Supplier")]);
                case "GetPaymentAsync": return Task.FromResult(new PaymentResponse { Id = PaymentId, InvoiceId = InvoiceId, VendorId = VendorId, PaymentNumber = "ACC-PAY-TEST-001", Amount = 1150, CurrencyCode = "ETB", ExchangeRate = 1, Direction = "Outgoing", Status = "Pending", PaymentMethod = "Cash", ReferenceNumber = "ACC-PAY-REF-001", PaymentDate = new DateOnly(2026, 11, 4) });
                case "CreatePaymentAsync": Saved = args![0]; return Task.FromResult(PaymentId);
                case "UpdatePaymentAsync": Saved = args![1]; return Task.CompletedTask;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
