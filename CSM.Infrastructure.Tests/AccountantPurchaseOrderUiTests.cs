using System.Reflection;
using Bunit;
using CSM.Domain.Enums;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AccountantPurchaseOrderUiTests
{
    [Theory]
    [InlineData("Draft", "Submit for Approval", "Approve")]
    [InlineData("PendingApproval", "Approve", "Send to Vendor")]
    [InlineData("Approved", "Send to Vendor", "SentToVendor")]
    public async Task Accountant_can_use_granted_purchase_order_workflow(string status, string button, string next)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["Accountant"], Enum.GetNames<GranularPermission>());
        var api = DispatchProxy.Create<IProcurementService, Api>();
        var data = (Api)(object)api; data.Order.Status = status;
        context.Services.AddSingleton(api);
        var page = context.RenderComponent<PurchaseOrderDetails>(p => p.Add(x => x.Id, data.Order.Id));
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == button));
        await page.FindAll("button").Single(b => b.TextContent.Trim() == button).ClickAsync(new());
        page.WaitForAssertion(() => Assert.Contains(next, page.Markup));
    }

    [Theory]
    [InlineData("Draft")] [InlineData("PendingApproval")]
    public void Revoked_workflow_grant_keeps_Accountant_details_read_only(string status)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["Accountant"], ["Sites_View", "Procurement_PurchaseOrder_View"]);
        var api = DispatchProxy.Create<IProcurementService, Api>();
        var data = (Api)(object)api; data.Order.Status = status;
        context.Services.AddSingleton(api);
        var page = context.RenderComponent<PurchaseOrderDetails>(p => p.Add(x => x.Id, data.Order.Id));
        page.WaitForAssertion(() => Assert.Contains("ACC-PO", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Submit") || b.TextContent.Trim() == "Approve");
    }

    public class Api : DispatchProxy
    {
        public PurchaseOrderResponse Order { get; } = new() { Id = Guid.NewGuid(), ConstructionSiteId = Guid.NewGuid(),
            PurchaseOrderNumber = "ACC-PO", VendorName = "Supplier", OrderDate = new(2026, 11, 5), Status = "Draft" };
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (args?[0] is not Guid id || id != Order.Id) throw new InvalidOperationException("Wrong purchase order identity.");
            if (method!.Name == nameof(IProcurementService.GetPurchaseOrderAsync)) return Task.FromResult<PurchaseOrderResponse?>(Order);
            Order.Status = method.Name switch {
                nameof(IProcurementService.SubmitPurchaseOrderForApprovalAsync) => "PendingApproval",
                nameof(IProcurementService.ApprovePurchaseOrderAsync) => "Approved",
                nameof(IProcurementService.SendPurchaseOrderToVendorAsync) => "SentToVendor",
                _ => throw new InvalidOperationException("Unexpected purchase order operation.")
            };
            return Task.CompletedTask;
        }
    }
}
