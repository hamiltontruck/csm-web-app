using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Editor = CSM.Web.Components.Pages.Finance.Sites.BudgetEdit;
using Details = CSM.Web.Components.Pages.Finance.Company.BudgetDetailsCompany;

namespace CSM.Infrastructure.Tests;

public sealed class CompanyBudgetEditNavigationTests
{
    private static readonly Guid BudgetId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondBudgetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Company_details_edit_button_targets_a_registered_editor_route()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<Details>(p => p.Add(x => x.Id, BudgetId));
        page.WaitForAssertion(() => Assert.Contains("ACC-BUD-TEST-001", page.Markup));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Edit Budget").Click();
        Assert.Equal($"http://localhost/finance/company/budgets/{BudgetId}/edit", context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Contains(typeof(Editor).GetCustomAttributes<RouteAttribute>(), r => r.Template == "/finance/company/budgets/{Id:guid}/edit");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Save_preserves_budget_fields_and_returns_to_its_record_scope(bool company)
    {
        using var context = Context(out var state);
        state.Company = company;
        var page = context.RenderComponent<Editor>(p => p.Add(x => x.Id, BudgetId).Add(x => x.SiteId, company ? Guid.Empty : SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Equal("Accountant Test Budget", page.Find(".col-md-8 input").GetAttribute("value"));
        page.Find("textarea").Change("Budget updated during Accountant testing.");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        Assert.Equal(BudgetId, state.SavedId);
        Assert.Equal("Accountant Test Budget", state.Saved!.Name);
        Assert.Equal("Budget updated during Accountant testing.", state.Saved.Description);
        Assert.Equal(5000m, state.Saved.TotalAmount);
        Assert.Equal("ETB", state.Saved.CurrencyCode);
        Assert.Equal(new DateOnly(2026, 11, 4), state.Saved.EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 11, 30), state.Saved.EffectiveTo);
        Assert.Equal("http://localhost" + DetailsPath(company), context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cancel_returns_to_the_record_scope_without_saving(bool company)
    {
        using var context = Context(out var state);
        state.Company = company;
        var page = context.RenderComponent<Editor>(p => p.Add(x => x.Id, BudgetId).Add(x => x.SiteId, company ? Guid.Empty : SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        Assert.Equal(0, state.SaveCalls);
        Assert.Equal("http://localhost" + DetailsPath(company), context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Denied_budget_load_does_not_render_an_edit_form_or_save()
    {
        using var context = Context(out var state);
        state.Denied = true;
        var page = context.RenderComponent<Editor>(p => p.Add(x => x.Id, BudgetId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".alert-danger")));
        Assert.Empty(page.FindAll("form, input, textarea"));
        Assert.Equal(0, state.SaveCalls);
    }

    [Fact]
    public void Changing_budget_route_reloads_the_form_before_saving_the_new_id()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<Editor>(p => p.Add(x => x.Id, BudgetId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.SetParametersAndRender(p => p.Add(x => x.Id, SecondBudgetId).Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal("Second Site Budget", page.Find(".col-md-8 input").GetAttribute("value")));
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(SecondBudgetId, state.SavedId));
        Assert.Equal("Second Site Budget", state.Saved!.Name);
        Assert.Equal(6000m, state.Saved.TotalAmount);
        Assert.Equal($"http://localhost/finance/sites/{SiteId}/budgets/{SecondBudgetId}", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task Late_previous_budget_load_cannot_replace_the_current_budget()
    {
        using var context = Context(out var state);
        state.DelayedFirst = new TaskCompletionSource<BudgetResponse>();
        var page = context.RenderComponent<Editor>(p => p.Add(x => x.Id, BudgetId));
        page.SetParametersAndRender(p => p.Add(x => x.Id, SecondBudgetId).Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal("Second Site Budget", page.Find(".col-md-8 input").GetAttribute("value")));
        await page.InvokeAsync(() => state.DelayedFirst.SetResult(new BudgetResponse { Id = BudgetId, Name = "Late company budget", TotalAmount = 5000m, CurrencyCode = "ETB", EffectiveFrom = new DateOnly(2026, 11, 4) }));
        page.WaitForAssertion(() => Assert.Equal("Second Site Budget", page.Find(".col-md-8 input").GetAttribute("value")));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        Assert.Equal($"http://localhost/finance/sites/{SiteId}/budgets/{SecondBudgetId}", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task Pending_save_keeps_its_payload_and_does_not_redirect_a_new_editor()
    {
        using var context = Context(out var state);
        state.PendingUpdate = new TaskCompletionSource<bool>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/finance/company/budgets/{BudgetId}/edit");
        var page = context.RenderComponent<Editor>(p => p.Add(x => x.Id, BudgetId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        var save = page.Find("form").SubmitAsync();
        try
        {
            var next = $"http://localhost/finance/sites/{SiteId}/budgets/{SecondBudgetId}/edit";
            navigation.NavigateTo(next);
            page.SetParametersAndRender(p => p.Add(x => x.Id, SecondBudgetId).Add(x => x.SiteId, SiteId));
            page.WaitForAssertion(() => Assert.Equal("Second Site Budget", page.Find(".col-md-8 input").GetAttribute("value")));
            Assert.Equal(BudgetId, state.SavedId);
            Assert.Equal("Accountant Test Budget", state.Saved!.Name);
            Assert.Equal(5000m, state.Saved.TotalAmount);
            state.PendingUpdate.SetResult(true);
            await save;
            Assert.Equal(next, navigation.Uri);
        }
        finally { state.PendingUpdate.TrySetResult(true); await save; }
    }

    private static string DetailsPath(bool company) => company
        ? $"/finance/company/budgets/{BudgetId}" : $"/finance/sites/{SiteId}/budgets/{BudgetId}";
    private static TestContext Context(out State state)
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["Accountant"]);
        state = new State();
        var api = DispatchProxy.Create<IFinanceService, Api>();
        ((Api)(object)api).State = state;
        context.Services.AddSingleton(api);
        return context;
    }
    public sealed class State { public bool Company = true; public bool Denied; public int SaveCalls; public Guid SavedId; public UpdateBudgetModel? Saved; public TaskCompletionSource<BudgetResponse>? DelayedFirst; public TaskCompletionSource<bool>? PendingUpdate; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IFinanceService.GetBudgetAsync))
            {
                var id = (Guid)args![0]!;
                if (id == BudgetId && State.DelayedFirst is not null) return State.DelayedFirst.Task;
                if (id == SecondBudgetId) return Task.FromResult(new BudgetResponse { Id = id, Name = "Second Site Budget", ConstructionSiteId = SiteId, TotalAmount = 6000m, CurrencyCode = "ETB", EffectiveFrom = new DateOnly(2026, 11, 4) });
                Assert.Equal(BudgetId, id);
                return State.Denied ? Task.FromException<BudgetResponse>(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden))
                    : Task.FromResult(new BudgetResponse { Id = BudgetId, BudgetNumber = "ACC-BUD-TEST-001", Name = "Accountant Test Budget",
                        ConstructionSiteId = State.Company ? null : SiteId, TotalAmount = 5000m, CurrencyCode = "ETB", Status = "Draft",
                        EffectiveFrom = new DateOnly(2026, 11, 4), EffectiveTo = new DateOnly(2026, 11, 30), Description = "Budget created during Accountant testing." });
            }
            if (method.Name == nameof(IFinanceService.GetBudgetLinesAsync)) return Task.FromResult(new List<BudgetLineResponse>());
            if (method.Name == nameof(IFinanceService.UpdateBudgetAsync))
            {
                State.SaveCalls++;
                State.SavedId = (Guid)args![0]!;
                State.Saved = (UpdateBudgetModel)args[1]!;
                return State.PendingUpdate?.Task ?? Task.CompletedTask;
            }
            throw new InvalidOperationException("Unexpected finance operation: " + method.Name);
        }
    }
}
