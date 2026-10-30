using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.Finance;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InvoiceActionVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Authorized_invoice_actions_keep_the_company_or_site_base_path(bool site)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["SuperAdmin", "FinanceOfficer"], ["Finance_Invoice_View"]);
        context.Services.AddSingleton(DispatchProxy.Create<IFinanceService, Finance>());
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, UnexpectedVendor>());
        var invoiceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var siteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var page = context.RenderComponent<InvoiceDetails>(p => p.Add(x => x.Id, invoiceId).Add(x => x.SiteId, site ? siteId : null));
        page.WaitForAssertion(() => Assert.Contains("INV-ACCESS", page.Markup));
        var edit = page.FindAll("button").Single(b => b.TextContent.Contains("Edit Invoice"));
        var line = page.FindAll("button").Single(b => b.TextContent.Contains("Add Line"));
        var expectedBase = site ? $"/finance/sites/{siteId}/invoices/{invoiceId}" : $"/finance/invoices/{invoiceId}";
        edit.Click();
        Assert.EndsWith(expectedBase + "/edit", context.Services.GetRequiredService<NavigationManager>().Uri);
        line.Click();
        Assert.EndsWith(expectedBase + "/lines/create", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    public class Finance : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IFinanceService.GetInvoiceAsync) => Task.FromResult<InvoiceResponse?>(new() { Id = (Guid)args![0]!, InvoiceNumber = "INV-ACCESS", Status = "Draft", InvoiceDate = new(2026, 10, 1) }),
            nameof(IFinanceService.GetInvoiceLinesAsync) => Task.FromResult(new List<InvoiceLineResponse>()),
            _ => throw new InvalidOperationException("Unexpected invoice mutation.")
        };
    }
    public class UnexpectedVendor : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new InvalidOperationException("Invoice has no vendor.");
    }
}
