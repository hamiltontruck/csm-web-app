using Bunit;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerReviewUiRegressionTests
{
    [Theory]
    [InlineData("budget")] [InlineData("invoice")] [InlineData("expense")] [InlineData("payment")]
    public void Mixed_accountant_reader_still_uses_authorized_site_form_options(string kind)
    {
        using var context = new TestContext(); WorkspaceTestAccess.Add(context,["FinanceOfficer","Accountant"],FinanceOfficerAuthorityTests.Defaults.Select(x=>x.ToString()).ToArray());
        var proxy=DispatchProxy.Create<IFinanceService,FinanceOfficerUiTests.Finance>();var finance=(FinanceOfficerUiTests.Finance)(object)proxy;context.Services.AddSingleton(proxy);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService,FinanceOfficerUiTests.Projects>());context.Services.AddSingleton(DispatchProxy.Create<IVendorService,FinanceOfficerUiTests.Vendors>());context.Services.AddSingleton(DispatchProxy.Create<IPayrollService,FinanceOfficerUiTests.Payroll>());
        var site=FinanceOfficerUiTests.Site;
        if(kind=="budget"){var p=context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.BudgetCreate>(a=>a.Add(x=>x.SiteId,site));p.WaitForAssertion(()=>Assert.Contains("Assigned project",p.Markup));}
        else if(kind=="invoice"){var p=context.RenderComponent<CSM.Web.Components.Pages.Finance.InvoiceCreate>(a=>a.Add(x=>x.SiteId,site));p.WaitForAssertion(()=>Assert.Contains("Assigned project",p.Markup));}
        else if(kind=="expense"){var p=context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseCreate>(a=>a.Add(x=>x.SiteId,site));p.WaitForAssertion(()=>Assert.Contains("Own vendor",p.Markup));}
        else {var p=context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentCreate>(a=>a.Add(x=>x.SiteId,(Guid?)site));p.WaitForAssertion(()=>Assert.DoesNotContain("Loading payment information",p.Markup));Assert.Empty(p.FindAll("[role=alert]"));}
        Assert.True(finance.OptionCalls>0);
    }
    [Fact]
    public void Rejected_petty_account_submission_preserves_fields_and_can_be_retried()
    {
        using var context=FinanceOfficerUiTests.Context(out var finance,out _);finance.FailOperation="CreatePettyCashAccountAsync";
        var page=context.RenderComponent<CSM.Web.Components.Pages.Finance.PettyCashAccountCreate>();page.WaitForAssertion(()=>Assert.Single(page.FindAll("form")));
        page.FindAll("input").ElementAt(0).Change("PC-RETRY");page.FindAll("input").ElementAt(2).Change("Preserved account");page.Find("form").Submit();
        page.WaitForAssertion(()=>Assert.NotEmpty(page.FindAll("[role=alert]")));Assert.Equal("PC-RETRY",page.FindAll("input").ElementAt(0).GetAttribute("value"));
        finance.FailOperation=null;page.Find("form").Submit();page.WaitForAssertion(()=>Assert.EndsWith($"/finance/petty-cash/site/{FinanceOfficerUiTests.Site}",context.Services.GetRequiredService<NavigationManager>().Uri));
    }
    [Fact]
    public void Cash_transaction_back_returns_to_the_loaded_accounts_site()
    {
        using var context=FinanceOfficerUiTests.Context(out _,out _);var page=context.RenderComponent<CSM.Web.Components.Pages.Finance.PettyCashTransactions>(p=>p.Add(x=>x.AccountId,Guid.NewGuid()));
        page.WaitForAssertion(()=>Assert.Contains("Own cash",page.Markup));page.FindAll("button").Single(x=>x.TextContent.Contains("Back")).Click();
        Assert.EndsWith($"/finance/petty-cash/site/{FinanceOfficerUiTests.Site}",context.Services.GetRequiredService<NavigationManager>().Uri);
    }
}
