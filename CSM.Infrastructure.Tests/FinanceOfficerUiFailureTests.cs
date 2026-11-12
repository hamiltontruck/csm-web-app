using Bunit;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerUiFailureTests
{
    [Theory]
    [InlineData("budget")] [InlineData("invoice")] [InlineData("payment")] [InlineData("expense")]
    public void API_failures_remain_local_to_the_finance_screen(string kind)
    {
        using var context = FinanceOfficerUiTests.Context(out var finance, out _); var site = FinanceOfficerUiTests.Site;
        if (kind == "budget") {
            finance.FailOperation = "CreateBudgetAsync";
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.BudgetCreate>(p => p.Add(x => x.SiteId, site));
            page.WaitForAssertion(() => Assert.Single(page.FindAll("form"))); page.Find("form").Submit();
            page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[role=alert]")));
        }
        else if (kind == "invoice") {
            finance.FailOperation = "GetSiteFormOptionsAsync";
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.InvoiceCreate>(p => p.Add(x => x.SiteId, site));
            page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[role=alert]")));
            Assert.True(page.FindAll("button").Single(x => x.TextContent.Contains("Save Invoice")).HasAttribute("disabled") || page.FindAll("fieldset[disabled]").Count > 0);
        }
        else if (kind == "payment") {
            finance.FailOperation = "ChangePaymentStatusAsync";
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentDetails>(p => p.Add(x => x.Id, Guid.NewGuid()).Add(x => x.SiteId, (Guid?)site));
            page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), x => x.TextContent.Contains("Complete Payment")));
            page.FindAll("button").Single(x => x.TextContent.Contains("Complete Payment")).Click();
            page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[role=alert]")));
        }
        else {
            finance.FailOperation = "ChangeExpenseStatusAsync";
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseDetails>(p => p.Add(x => x.ExpenseId, Guid.NewGuid()).Add(x => x.SiteId, site));
            page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), x => x.TextContent.Contains("Submit Expense")));
            page.FindAll("button").Single(x => x.TextContent.Contains("Submit Expense")).Click();
            page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[role=alert]")));
        }
    }
    [Theory]
    [InlineData("invoice")] [InlineData("payment")]
    public void Opening_another_finance_record_replaces_the_previous_record_before_actions(string kind)
    {
        using var context = FinanceOfficerUiTests.Context(out var finance, out _); finance.EchoIds = true;
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var site = FinanceOfficerUiTests.Site;
        if (kind == "invoice") {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.InvoiceDetails>(p => p.Add(x => x.Id, first).Add(x => x.SiteId, (Guid?)site));
            page.WaitForAssertion(() => Assert.Contains(first.ToString(), page.Markup));
            page.SetParametersAndRender(p => p.Add(x => x.Id, second));
            page.WaitForAssertion(() => Assert.Contains(second.ToString(), page.Markup));
            Assert.DoesNotContain(first.ToString(), page.Markup);
        }
        else {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentDetails>(p => p.Add(x => x.Id, first).Add(x => x.SiteId, (Guid?)site));
            page.WaitForAssertion(() => Assert.Contains(first.ToString(), page.Markup));
            page.SetParametersAndRender(p => p.Add(x => x.Id, second));
            page.WaitForAssertion(() => Assert.Contains(second.ToString(), page.Markup));
            Assert.DoesNotContain(first.ToString(), page.Markup);
        }
    }

}
