using System.Reflection;
using Bunit;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class FinanceCostCodeActionUiTests
{
    [Theory]
    [InlineData("FinanceOfficer", true, false)]
    [InlineData("FinanceOfficer", false, false)]
    [InlineData("Accountant", true, false)]
    [InlineData("Accountant", true, true)]
    [InlineData("Accountant", false, true)]
    [InlineData("CompanyAdmin", false, false)]
    [InlineData("CompanyAdmin", false, true)]
    public void Reader_sees_codes_without_status_mutation_controls(string role, bool active, bool company)
    {
        using var context = Create(role, false, active, out _);
        var page = Render(context, company);
        page.WaitForAssertion(() => Assert.Contains("General Construction", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Activate") || b.TextContent.Contains("Deactivate"));
        Assert.Contains(active ? "Active" : "Inactive", page.Markup);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Manager_with_live_grant_can_change_code_status(bool company)
    {
        using var context = Create("Accountant", true, true, out var finance);
        var page = Render(context, company);
        page.FindAll("button").Single(b => b.TextContent.Contains("Deactivate")).Click();
        page.WaitForAssertion(() => Assert.Contains("Inactive", page.Markup));
        Assert.Equal((finance.Code.Id, false), finance.Change);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Rejected_status_change_shows_local_error_and_preserves_status(bool company)
    {
        using var context = Create("Accountant", true, true, out var finance);
        finance.Reject = true;
        var page = Render(context, company);
        page.FindAll("button").Single(b => b.TextContent.Contains("Deactivate")).Click();
        page.WaitForAssertion(() => Assert.Contains("unavailable", page.Find("[role=alert]").TextContent));
        Assert.True(finance.Code.IsActive);
    }

    private static TestContext Create(string role, bool manage, bool active, out Finance finance)
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context, [role], manage ? ["Finance_CostCode_View", "Finance_CostCode_Manage"] : ["Finance_CostCode_View"]);
        var service = DispatchProxy.Create<IFinanceService, Finance>();
        finance = (Finance)(object)service;
        finance.Code.IsActive = active;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IOfflineSyncService, Online>());
        return context;
    }

    private static IRenderedFragment Render(TestContext context, bool company) => company
        ? context.RenderComponent<CSM.Web.Components.Pages.Finance.Company.CostCodes>()
        : context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.CostCodes>(p => p.Add(x => x.SiteId, FinanceOfficerUiTests.Site));

    public class Online : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "IsOnlineAsync" ? Task.FromResult(true) : throw new InvalidOperationException(method.Name);
    }

    public class Finance : DispatchProxy
    {
        public CostCodeResponse Code = new() { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), ConstructionSiteId = FinanceOfficerUiTests.Site, Code = "HBG-BUD-001", Name = "General Construction", IsActive = true };
        public (Guid Id, bool Active)? Change;
        public bool Reject;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name is "GetSiteCostCodesAsync" or "GetCostCodesAsync") return Task.FromResult<IReadOnlyList<CostCodeResponse>>([Code]);
            if (method.Name == "ChangeCostCodeStatusAsync")
            {
                if (Reject) return Task.FromException(new HttpRequestException("Cost code action unavailable."));
                Change = ((Guid)args![0]!, (bool)args[1]!);
                return Task.CompletedTask;
            }
            throw new InvalidOperationException(method.Name);
        }
    }
}
