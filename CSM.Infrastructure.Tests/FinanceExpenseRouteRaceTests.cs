using System.Reflection;
using Bunit;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class FinanceExpenseRouteRaceTests
{
    [Theory]
    [InlineData("different-record")] [InlineData("new-action")]
    [InlineData("return-to-same-record")]
    public async Task Old_expense_action_cannot_leave_or_overwrite_a_new_route_action_state(string scenario)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["CompanyAdmin"], ["Finance_Expense_View", "Finance_Expense_Submit", "Finance_Expense_Approve"]);
        var api = DispatchProxy.Create<IFinanceService, Slow>(); var state = (Slow)(object)api; state.Status = "Draft";
        context.Services.AddSingleton(api);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, FinanceSettlementUiTests.Forbidden>());
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, FinanceSettlementUiTests.Forbidden>());
        var first = state.Id; var second = Guid.NewGuid();
        var page = context.RenderComponent<CSM.Web.Components.Pages.Finance.Company.ExpenseDetails>(p => p.Add(x => x.id, first));
        page.WaitForAssertion(() => Assert.Contains("EXP-CHECK", page.Markup));
        Task? pendingB = null;
        var pendingA = Submit(page).ClickAsync(new MouseEventArgs());
        try
        {
            page.WaitForAssertion(() => Assert.Single(state.Requests));
            page.SetParametersAndRender(p => p.Add(x => x.id, second));
            page.WaitForAssertion(() => Assert.False(Submit(page).HasAttribute("disabled")));
            if (scenario == "return-to-same-record")
            {
                page.SetParametersAndRender(p => p.Add(x => x.id, first));
                page.WaitForAssertion(() => Assert.False(Submit(page).HasAttribute("disabled")));
                state.Requests[0].SetException(new HttpRequestException("Old route failure"));
                await pendingA;
                Assert.DoesNotContain("Unable to update this expense", page.Markup);
                Assert.False(Submit(page).HasAttribute("disabled"));
            }
            else if (scenario == "new-action")
            {
                pendingB = Submit(page).ClickAsync(new MouseEventArgs());
                page.WaitForAssertion(() => Assert.Equal(2, state.Requests.Count));
                Assert.True(Submit(page).HasAttribute("disabled"));
                state.Requests[0].SetResult(); await pendingA;
                Assert.True(Submit(page).HasAttribute("disabled"));
                state.Requests[1].SetResult(); await pendingB;
                Assert.False(Submit(page).HasAttribute("disabled"));
            }
            else
            {
                state.Requests[0].SetResult(); await pendingA;
                Assert.False(Submit(page).HasAttribute("disabled"));
                Assert.DoesNotContain("Unable to update this expense", page.Markup);
            }
        }
        finally
        {
            foreach (var request in state.Requests) request.TrySetResult();
            await pendingA; if (pendingB is not null) await pendingB;
        }
    }
    private static AngleSharp.Dom.IElement Submit(IRenderedFragment page) => page.FindAll("button").Single(x => x.TextContent.Contains("Submit Expense"));
    public class Slow : FinanceSettlementUiTests.Probe
    {
        public List<TaskCompletionSource> Requests = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != "ChangeExpenseStatusAsync") return base.Invoke(method, args);
            var request = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); Requests.Add(request); return request.Task;
        }
    }
}
