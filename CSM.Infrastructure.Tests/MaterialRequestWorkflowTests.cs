using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class MaterialRequestWorkflowTests
{
    [Fact]
    public async Task Draft_can_be_submitted_then_partially_approved_with_each_line_id()
    {
        using var context = Context();
        var page = context.RenderComponent<MaterialRequestDetails>(p => p.Add(x => x.Id, RequestId));
        await page.Find("#submit-material-request").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Equal("Submitted", page.Find("#request-status").TextContent));
        page.Find("input[type=number]").Change("6");
        await page.Find("#approve-material-request").ClickAsync(new());
        var proxy = (ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>();
        var approval = Assert.Single(proxy.Approvals);
        var line = Assert.Single(approval.GetProperty("Lines").EnumerateArray());
        Assert.Equal(LineId, line.GetProperty("MaterialRequestLineId").GetGuid());
        Assert.Equal(6m, line.GetProperty("ApprovedQuantity").GetDecimal());
        Assert.Equal("PartiallyApproved", page.Find("#request-status").TextContent);
        Assert.Empty(page.FindAll("#approve-material-request"));
    }

    [Fact]
    public async Task Api_rejection_keeps_request_and_allows_retry()
    {
        using var context = Context();
        var proxy = (ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>();
        proxy.Error = "Only Draft material requests can be submitted.";
        var page = context.RenderComponent<MaterialRequestDetails>(p => p.Add(x => x.Id, RequestId));
        await page.Find("#submit-material-request").ClickAsync(new());
        Assert.Contains(proxy.Error, page.Find("[role=alert]").TextContent);
        Assert.Contains("MR-2026-001", page.Markup);
        Assert.False(page.Find("#submit-material-request").HasAttribute("disabled"));
    }

    [Fact]
    public void Read_only_user_has_no_workflow_buttons()
    {
        using var context = Context();
        ((AuthProxy)(object)context.Services.GetRequiredService<IAuthService>()).Permissions = [];
        var page = context.RenderComponent<MaterialRequestDetails>(p => p.Add(x => x.Id, RequestId));
        Assert.Empty(page.FindAll("#submit-material-request"));
        Assert.Empty(page.FindAll("#approve-material-request"));
    }

    [Fact]
    public async Task Approval_above_requested_quantity_is_rejected_before_sending()
    {
        using var context = Context();
        var proxy = (ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>();
        proxy.Request.Status = "Submitted";
        var page = context.RenderComponent<MaterialRequestDetails>(p => p.Add(x => x.Id, RequestId));
        page.Find("input[type=number]").Change("11");
        await page.Find("#approve-material-request").ClickAsync(new());
        Assert.Contains("cannot exceed", page.Find("[role=alert]").TextContent);
        Assert.Empty(proxy.Approvals);
    }

    [Fact]
    public void Completed_request_has_no_submit_or_approve_action()
    {
        using var context = Context();
        ((ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>()).Request.Status = "Approved";
        var page = context.RenderComponent<MaterialRequestDetails>(p => p.Add(x => x.Id, RequestId));
        Assert.Empty(page.FindAll("#submit-material-request"));
        Assert.Empty(page.FindAll("#approve-material-request"));
    }

    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid LineId = Guid.NewGuid();

    private static TestContext Context()
    {
        var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IProcurementService, ProcurementProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, AuthProxy>());
        return context;
    }

    public class AuthProxy : DispatchProxy
    {
        public IReadOnlyCollection<string> Permissions { get; set; } =
            ["Procurement_MaterialRequest_Submit", "Procurement_MaterialRequest_Approve"];
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(Permissions);
    }

    public class ProcurementProxy : DispatchProxy
    {
        public MaterialRequestResponse Request { get; } = new()
        {
            Id = RequestId, RequestNumber = "MR-2026-001", Status = "Draft",
            SiteName = "Hamilton Test Site",
            Lines = [new() { Id = LineId, MaterialName = "Cement", UnitOfMeasure = "Bag", RequestedQuantity = 10m }]
        };
        public string? Error { get; set; }
        public List<JsonElement> Approvals { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetMaterialRequestAsync")
                return Task.FromResult<MaterialRequestResponse?>(Request);
            if (Error is not null)
                return Task.FromException<MaterialRequestResponse>(new HttpRequestException(Error));
            if (method.Name == "SubmitMaterialRequestAsync")
                Request.Status = "Submitted";
            else if (method.Name == "ApproveMaterialRequestAsync")
            {
                var json = JsonSerializer.SerializeToElement(args![1]);
                Approvals.Add(json);
                Request.Status = json.GetProperty("Lines")[0].GetProperty("ApprovedQuantity").GetDecimal() < 10m
                    ? "PartiallyApproved" : "Approved";
            }
            else throw new NotSupportedException(method.Name);
            return Task.FromResult(Request);
        }
    }
}
