using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class FinanceSettlementUiTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Budget_approval_uses_server_capability_and_explains_denial(bool company, bool canApprove)
    {
        using var context = Context(out var state, company ? "CompanyAdmin" : "FinanceOfficer"); state.CanApprove = canApprove;
        IRenderedFragment page = company
            ? context.RenderComponent<CSM.Web.Components.Pages.Finance.Company.BudgetDetailsCompany>(p => p.Add(x => x.Id, state.Id))
            : context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.BudgetDetails>(p => p.Add(x => x.Id, state.Id).Add(x => x.SiteId, state.Site));
        page.WaitForAssertion(() => Assert.Contains("BUD-CHECK", page.Markup));
        var approve = page.FindAll("button").Single(x => x.TextContent.Trim() == "Approve");
        Assert.Equal(!canApprove, approve.HasAttribute("disabled"));
        if (!canApprove) Assert.Contains("Ask another authorized person", page.Markup);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Expense_approval_uses_server_capability(bool canApprove)
    {
        using var context = Context(out var state); state.CanApprove = canApprove; state.Status = "Submitted";
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseDetails>(p => p.Add(x => x.ExpenseId, state.Id).Add(x => x.SiteId, state.Site));
        page.WaitForAssertion(() => Assert.Contains("EXP-CHECK", page.Markup));
        Assert.Equal(!canApprove, page.FindAll("button").Single(x => x.TextContent.Contains("Approve Expense")).HasAttribute("disabled"));
        if (!canApprove) Assert.Contains("Ask another authorized person", page.Markup);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Partial_and_legacy_states_are_truthful_and_have_no_status_only_paid_action(bool legacy)
    {
        using var context = Context(out var state); state.Status = legacy ? "Paid" : "Approved"; state.Legacy = legacy;
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseDetails>(p => p.Add(x => x.ExpenseId, state.Id).Add(x => x.SiteId, state.Site));
        page.WaitForAssertion(() => Assert.Contains(legacy ? "unverified" : "Outstanding", page.Markup));
        Assert.DoesNotContain("Mark as Paid", page.Markup);
        if (legacy) Assert.DoesNotContain("Record Expense Payment", page.Markup);
        else { Assert.Contains("40.00", page.Markup); Assert.Contains("PAY-EVIDENCE", page.Markup); Assert.Contains("Record Expense Payment", page.Markup); }
    }

    [Fact]
    public void Expense_payment_preserves_partial_input_on_error_and_route_switch_discards_stale_target()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentCreate>(ComponentParameter.CreateParameter("SiteId", state.Site), ComponentParameter.CreateParameter("ExpenseId", state.Id));
        page.WaitForAssertion(() => Assert.Contains("EXP-CHECK", page.Markup));
        var number = page.Find("input[placeholder='Enter payment number']");
        number.Change("PAY-PARTIAL");
        page.Find("input[type=number]").Change("20"); page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("rejected for test", page.Markup));
        Assert.Contains("20", page.Find("input[type=number]").GetAttribute("value"));
        Assert.Equal(state.Id, JsonSerializer.SerializeToElement(state.Submitted).GetProperty("ExpenseId").GetGuid());
        Assert.Equal(20m, JsonSerializer.SerializeToElement(state.Submitted).GetProperty("Amount").GetDecimal());
        var next = Guid.NewGuid(); state.Id = next;
        page.SetParametersAndRender(ComponentParameter.CreateParameter("ExpenseId", next));
        page.WaitForAssertion(() => Assert.DoesNotContain("PAY-PARTIAL", page.Markup));
        Assert.DoesNotContain(state.Calls, x => x.Contains("Payroll") || x.Contains("VendorsAsync"));
    }

    [Fact]
    public void Expense_cash_out_keeps_partial_fields_after_error_and_account_switch_blocks_stale_expense()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PettyCashTransactionCreate>(
            ComponentParameter.CreateParameter("AccountId", state.Account), ComponentParameter.CreateParameter("ExpenseId", state.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.FindAll("input").ElementAt(0).Change("CASH-REF");
        page.Find("input[type=number]").Change("10");
        page.FindAll("input").Last().Change("Partial expense");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("rejected for test", page.Markup));
        var body = JsonSerializer.SerializeToElement(state.Submitted);
        Assert.Equal(state.Id, body.GetProperty("ExpenseId").GetGuid()); Assert.Equal(10m, body.GetProperty("Amount").GetDecimal());
        Assert.Equal("10", page.Find("input[type=number]").GetAttribute("value"));
        Assert.Equal("CASH-REF", page.FindAll("input").ElementAt(0).GetAttribute("value"));
        var calls = state.Calls.Count(x => x == "CreatePettyCashTransactionAsync");
        page.SetParametersAndRender(ComponentParameter.CreateParameter("AccountId", Guid.NewGuid()), ComponentParameter.CreateParameter("ExpenseId", state.Id));
        page.WaitForAssertion(() => Assert.Contains("not available for this cash account", page.Markup));
        page.Find("form").Submit(); Assert.Equal(calls, state.Calls.Count(x => x == "CreatePettyCashTransactionAsync"));
    }

    [Fact]
    public void Pending_expense_payment_edit_preserves_its_link_and_site_on_company_route()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.PaymentEdit>(p => p.Add(x => x.Id, state.Id));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Submitted));
        var body = JsonSerializer.SerializeToElement(state.Submitted);
        Assert.Equal(state.Id, body.GetProperty("ExpenseId").GetGuid()); Assert.Equal(state.Site, body.GetProperty("ConstructionSiteId").GetGuid());
    }

    private static TestContext Context(out Probe state, string role = "FinanceOfficer")
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context, [role], ["Sites_View", "Finance_Budget_View", "Finance_Budget_Manage", "Finance_Expense_View", "Finance_Expense_Submit", "Finance_Expense_Approve", "Finance_Payment_View", "Finance_Payment_Manage", "Finance_PettyCash_View", "Finance_PettyCash_Manage"]);
        var service = DispatchProxy.Create<IFinanceService, Probe>(); state = (Probe)(object)service; context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Forbidden>());
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, Forbidden>());
        context.Services.AddSingleton(DispatchProxy.Create<IPayrollService, Forbidden>());
        return context;
    }
    public class Forbidden : DispatchProxy { protected override object? Invoke(MethodInfo? m, object?[]? a) => throw new InvalidOperationException("Unexpected broad lookup " + m!.Name); }
    public class Probe : DispatchProxy
    {
        public Guid Id = Guid.NewGuid(), Site = Guid.NewGuid(), Company = Guid.NewGuid(), Account = Guid.NewGuid(), Cost = Guid.NewGuid();
        public bool CanApprove, Legacy; public string Status = "Approved"; public object? Submitted; public List<string> Calls = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var m = method!; Calls.Add(m.Name);
            if (m.Name == "CreatePettyCashTransactionAsync") { Submitted = args![0]; return Task.FromException<Guid>(new HttpRequestException("Cash Out rejected for test")); }
            if (m.Name == "UpdatePaymentAsync") { Submitted = args![1]; return Task.CompletedTask; }
            if (m.Name == "CreatePaymentAsync") { Submitted = args![0]; return Task.FromException<Guid>(new HttpRequestException("Payment rejected for test")); }
            object? payload = m.Name switch
            {
                "GetPettyCashAccountAsync" => new { Id = (Guid)args![0]!, CompanyId = Company, ConstructionSiteId = Site, AccountCode = "CASH", AccountName = "Cash", CurrentBalance = 100m, IsActive = true },
                "GetPettyCashCostCodeOptionsAsync" => new[] { new { Id = Cost, Code = "COST", Name = "General" } },
                "GetPaymentAsync" => new { Id, ExpenseId = Id, CompanyId = Company, ConstructionSiteId = Site, PaymentNumber = "PENDING", PaymentDate = "2026-11-13", Amount = 20m, CurrencyCode = "ETB", ExchangeRate = 1m, Direction = "Outgoing", Status = "Pending" },
                "GetBudgetAsync" => new { Id, ConstructionSiteId = Site, BudgetNumber = "BUD-CHECK", Status = "PendingApproval", CanApprove, ApprovalBlockReason = CanApprove ? null : "Ask another authorized person" },
                "GetBudgetLinesAsync" => Array.Empty<object>(),
                "GetActionCapabilitiesAsync" => new { CanUsePayroll = false },
                "GetSiteFormOptionsAsync" => new { SiteId = Site, CompanyId = Company, SiteName = "Assigned", Projects = Array.Empty<object>(), Vendors = Array.Empty<object>(), CostCodes = Array.Empty<object>() },
                "GetExpenseAsync" => new { Id = (Guid)args![0]!, CompanyId = Company, ConstructionSiteId = Site, CostCodeId = Cost, ExpenseNumber = "EXP-CHECK", ExpenseDate = "2026-11-13", Description = "Expense", Amount = 50m, TaxAmount = 10m, CurrencyCode = "ETB", ExchangeRate = 1m, Status, CanApprove, ApprovalBlockReason = CanApprove ? null : "Ask another authorized person", Settlement = new { TotalDue = 60m, SettledAmount = Legacy ? 0m : 20m, OutstandingAmount = Legacy ? (decimal?)null : 40m, IsVerified = !Legacy, IsLegacyPaid = Legacy, State = Legacy ? "LegacyPaid" : "Partial", Evidence = Legacy ? [] : new[] { new { Source = "Payment", Id = Guid.NewGuid(), Number = "PAY-EVIDENCE", Status = "Completed", Amount = 20m, CurrencyCode = "ETB" } } } },
                "GetExpenseSettlementOptionsAsync" => new { Expense = Option(), CashAccounts = new[] { new { Id = Account, AccountCode = "CASH", AccountName = "Site Cash", CurrentBalance = 100m } } },
                "GetExpenseSettlementChoicesAsync" => args![2] is Guid account && account != Account ? Array.Empty<object>() : new[] { Option() },
                _ => throw new InvalidOperationException("Unexpected scoped lookup " + m.Name)
            };
            var type = m.ReturnType.GetGenericArguments().Single(); var value = JsonSerializer.Deserialize(JsonSerializer.Serialize(payload), type);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, [value]);
        }
        private object Option() => new { Id, ExpenseNumber = "EXP-CHECK", CompanyId = Company, ConstructionSiteId = Site, CostCodeId = Cost, CurrencyCode = "ETB", ExchangeRate = 1m, TotalDue = 60m, OutstandingAmount = 40m };
    }
}
