using System.Reflection;
using Bunit;
using CSM.Web.Models.Finance;
using CSM.Web.Models.Projects;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using CSM.Web.Services.Access;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerUiTests
{
    internal static readonly Guid Site = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly string[] Grants = FinanceOfficerAuthorityTests.Defaults.Select(x => x.ToString()).ToArray();
    [Theory]
    [InlineData("/finance/sites/{siteId}/invoices", true)]
    [InlineData("/finance/sites/{siteId}/invoices/create", true)]
    [InlineData("/finance/sites/{siteId}/payments/create", true)]
    [InlineData("/finance/sites/{siteId}/expenses/create", true)]
    [InlineData("/finance/sites/{siteId}/cost-codes", true)]
    [InlineData("/finance/company/budgets", false)]
    [InlineData("/finance/company/invoices", false)]
    [InlineData("/finance/company/cost-codes", false)]
    [InlineData("/finance/petty-cash", false)]
    [InlineData("/sites-management", true)]
    [InlineData("/users", false)] [InlineData("/hr/payroll", false)]
    public void New_role_navigation_matches_site_finance_contract(string path, bool visible) =>
        Assert.Equal(visible, new WorkspaceAccess(["FinanceOfficer"], Grants).CanVisit(path));

    [Theory]
    [InlineData("budget")] [InlineData("invoice")] [InlineData("expense")] [InlineData("payment")]
    public void Officer_forms_use_finance_owned_options_without_project_procurement_or_payroll_lookup(string pageKind)
    {
        using var context = Context(out var finance, out var payroll);
        if (pageKind == "budget") {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.BudgetCreate>(p => p.Add(x => x.SiteId, Site));
            page.WaitForAssertion(() => Assert.Contains("Assigned project", page.Markup));
        }
        else if (pageKind == "invoice") {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.InvoiceCreate>(p => p.Add(x => x.SiteId, Site));
            page.WaitForAssertion(() => Assert.Contains("Assigned project", page.Markup));
        }
        else if (pageKind == "expense") {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseCreate>(p => p.Add(x => x.SiteId, Site));
            page.WaitForAssertion(() => Assert.Contains("Assigned project", page.Markup));
            Assert.Contains("Own vendor", page.Markup);
        }
        else {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentCreate>(p => p.Add(x => x.SiteId, (Guid?)Site));
            page.WaitForAssertion(() => Assert.DoesNotContain("Loading payment information", page.Markup));
            Assert.DoesNotContain("<option value=\"Payroll\">", page.Markup);
            Assert.Equal(0, payroll.Calls);
            Assert.Empty(page.FindAll("[role=alert]"));
        }
        Assert.True(finance.OptionCalls > 0);
    }

    [Theory]
    [InlineData("invoice-edit")] [InlineData("expense-edit")]
    public void Officer_edit_forms_load_scoped_finance_options(string kind)
    {
        using var context = Context(out var finance, out _);
        if (kind == "invoice-edit") {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.InvoiceEdit>(p => p.Add(x => x.Id, Guid.NewGuid()));
            page.WaitForAssertion(() => Assert.Contains("Assigned project", page.Markup));
        }
        else {
            var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseEdit>(p => p.Add(x => x.ExpenseId, Guid.NewGuid()).Add(x => x.SiteId, Site));
            page.WaitForAssertion(() => Assert.Contains("Own vendor", page.Markup));
        }
        Assert.True(finance.OptionCalls > 0);
    }

    [Fact]
    public void Officer_editing_payment_through_generic_route_preserves_its_actual_site()
    {
        using var context = Context(out var finance, out _);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentEdit>(p => p.Add(x => x.Id, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(finance.PaymentUpdate));
        Assert.Equal(Site, finance.PaymentUpdate!.ConstructionSiteId);
    }

    [Fact]
    public void Officer_finance_hub_has_an_assigned_sites_entry_without_placeholder_company_totals()
    {
        using var context = Context(out _, out _);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Company.FinanceDashboard>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("a[href='/finance/sites']")));
        Assert.DoesNotContain("Finance Overview", page.Markup);
    }

    [Fact]
    public void Officer_site_expense_can_submit_approve_and_mark_paid_using_real_actions()
    {
        using var context = Context(out var finance, out _);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseDetails>(p => p.Add(x => x.ExpenseId, Guid.NewGuid()).Add(x => x.SiteId, Site));
        page.WaitForAssertion(() => Assert.Contains("E-1", page.Markup));
        foreach (var label in new[] { "Submit Expense", "Approve Expense", "Mark as Paid" }) {
            page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), x => x.TextContent.Contains(label)));
            page.FindAll("button").Single(x => x.TextContent.Contains(label)).Click();
        }
        Assert.Equal(new[] { 2, 3, 5 }, finance.ExpenseActions);
    }

    [Fact]
    public void Officer_cash_account_creation_offers_only_assigned_site_context()
    {
        using var context = Context(out _, out _);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PettyCashAccountCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.DoesNotContain("Company Level", page.Markup);
        page.FindAll("button").Single(x => x.TextContent.Contains("Back")).Click();
        Assert.False(context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri.EndsWith("/finance/petty-cash", StringComparison.Ordinal));
    }

    [Fact]
    public void Officer_site_invoice_displays_edit_issue_and_line_actions()
    {
        using var context = Context(out _, out _);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.InvoiceDetails>(p => p.Add(x => x.Id, Guid.NewGuid()).Add(x => x.SiteId, (Guid?)Site));
        page.WaitForAssertion(() => Assert.Contains("INV-OFFICER", page.Markup));
        Assert.Contains(page.FindAll("button"), x => x.TextContent.Contains("Edit Invoice"));
        Assert.Contains(page.FindAll("button"), x => x.TextContent.Contains("Add Line"));
        Assert.Contains(page.FindAll("button"), x => x.TextContent.Contains("Issue Invoice"));
    }

    internal static TestContext Context(out Finance finance, out Payroll payroll)
    {
        var context = new TestContext(); WorkspaceTestAccess.Add(context, ["FinanceOfficer"], Grants);
        var proxy = DispatchProxy.Create<IFinanceService, Finance>(); finance = (Finance)(object)proxy; context.Services.AddSingleton(proxy);
        var payrollProxy = DispatchProxy.Create<IPayrollService, Payroll>(); payroll = (Payroll)(object)payrollProxy; context.Services.AddSingleton(payrollProxy);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Projects>());
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, Vendors>());
        return context;
    }
    public class Finance : DispatchProxy
    {
        public int OptionCalls;
        public string? FailOperation;
        public bool EchoIds;
        public UpdatePaymentRequest? PaymentUpdate;
        public string ExpenseStatus = "Draft";
        public List<int> ExpenseActions = [];
        protected override object? Invoke(MethodInfo? m, object?[]? a) {
            if (m!.Name == FailOperation) throw new HttpRequestException("The requested finance action is unavailable.", null, System.Net.HttpStatusCode.Forbidden);
            if (m.Name == "GetSiteFormOptionsAsync") { OptionCalls++; return Task.FromResult(new FinanceSiteOptions {
                SiteId = Site, SiteCode = "A", SiteName = "Assigned site", Projects = [new() { Id = Guid.NewGuid(), Name = "Assigned project", ProjectCode = "P", ConstructionSiteId = Site }],
                CostCodes = [new() { Id = Guid.NewGuid(), Code = "SHARED", Name = "Shared code", IsActive = true }],
                Vendors = [new() { Id = Guid.NewGuid(), VendorCode = "OWN", Name = "Own vendor", IsActive = true }] }); }
            if (m.Name == "ChangeExpenseStatusAsync") { var status = (int)a![1]!; ExpenseActions.Add(status); ExpenseStatus = status switch { 2 => "Submitted", 3 => "Approved", 5 => "Paid", _ => throw new InvalidOperationException() }; return Task.CompletedTask; }
            if (m.Name == "UpdatePaymentAsync") { PaymentUpdate = (UpdatePaymentRequest)a![1]!; return Task.CompletedTask; }
            return m.Name switch {
                "GetExpenseAsync" => Task.FromResult(new ExpenseResponse((Guid)a![0]!, Guid.NewGuid(), Site, null, Guid.NewGuid(), null, "E-1", new(2026,11,11), "Expense", 1m, 0m, "ETB", 1m, null, null, ExpenseStatus, null, null, null, DateTime.UtcNow, null)),
                "GetPaymentAsync" => Task.FromResult(new PaymentResponse { Id = (Guid)a![0]!, ConstructionSiteId = Site, PaymentNumber = EchoIds ? ((Guid)a![0]!).ToString() : "P-1", Amount = 1m, ExchangeRate = 1m, CurrencyCode = "ETB", Direction = "Outgoing", Status = "Pending", PaymentDate = new(2026,11,11) }),
                "GetInvoiceAsync" => Task.FromResult<InvoiceResponse?>(new() { Id = (Guid)a![0]!, ConstructionSiteId = Site, InvoiceNumber = EchoIds ? ((Guid)a![0]!).ToString() : "INV-OFFICER", Status = "Draft", InvoiceDate = new(2026,11,11) }),
                "GetInvoiceLinesAsync" => Task.FromResult(new List<InvoiceLineResponse>()),
                "GetInvoiceVendorOptionsAsync" => Task.FromResult<IReadOnlyList<InvoiceVendorOption>>([]),
                _ => throw new InvalidOperationException("Unexpected finance lookup: " + m.Name)
            };
        }
    }
    public class Auth : DispatchProxy {
        protected override object? Invoke(MethodInfo? m, object?[]? a) => m!.Name switch {
            "GetRolesAsync" => Task.FromResult<IReadOnlyCollection<string>>(["FinanceOfficer"]),
            "GetPermissionsAsync" => Task.FromResult<IReadOnlyCollection<string>>(Grants),
            _ => throw new InvalidOperationException(m.Name)
        };
    }
    public class Projects : DispatchProxy {
        protected override object? Invoke(MethodInfo? m, object?[]? a) => m!.Name == "GetSitesAsync" ? Task.FromResult(new List<SiteResponse> { new() { Id = Site, Name = "Assigned site", IsActive = true } }) : m.Name == "GetSiteAsync"
            ? Task.FromResult<SiteResponse?>(new() { Id = Site, Name = "Assigned site", SiteCode = "A", IsActive = true })
            : throw new InvalidOperationException("Finance role must use finance-owned project options.");
    }
    public class Vendors : DispatchProxy { protected override object? Invoke(MethodInfo? m, object?[]? a) => throw new InvalidOperationException("Finance role must not load procurement vendor data."); }
    public class Payroll : DispatchProxy { public int Calls; protected override object? Invoke(MethodInfo? m, object?[]? a) { Calls++; throw new InvalidOperationException("Finance role must not load payroll data."); } }
}
