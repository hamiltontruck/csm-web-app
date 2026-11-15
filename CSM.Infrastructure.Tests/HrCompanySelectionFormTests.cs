using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.Common;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class HrCompanySelectionFormTests
{
    [Theory]
    [InlineData("shift")]
    [InlineData("contractor")]
    public void Company_selection_is_submitted_with_the_create_request(string flow)
    {
        using var context = Context(out var state);
        var page = Render(context, flow);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("select#" + flow + "-company")));
        page.Find("#" + flow + "-company").Change(state.Second.ToString());
        if (flow == "shift")
        {
            page.FindAll("input").ToArray()[0].Change("DAY");
            page.FindAll("input").ToArray()[1].Change("Day");
            page.Find("form").Submit();
        }
        else
        {
            page.FindAll("input").ToArray()[0].Change("C1");
            page.FindAll("input").ToArray()[1].Change("Contractor");
            page.FindAll("button").Single(x => x.TextContent.Contains("Save Contractor")).Click();
        }
        page.WaitForAssertion(() => Assert.Equal(state.Second, state.Selected));
    }

    [Theory]
    [InlineData("shift", 401)]
    [InlineData("shift", 403)]
    [InlineData("shift", 500)]
    [InlineData("contractor", 401)]
    [InlineData("contractor", 403)]
    [InlineData("contractor", 500)]
    public void Options_failure_has_safe_feedback_and_prevents_partial_submission(string flow, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, flow);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, select"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Null(state.Selected);
    }

    private static IRenderedFragment Render(TestContext context, string flow) => flow == "shift"
        ? context.RenderComponent<ShiftCreate>() : context.RenderComponent<ContractorCreate>();
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new State();
        var shifts = DispatchProxy.Create<IShiftService, Api>(); ((Api)(object)shifts).State = state;
        var contractors = DispatchProxy.Create<IContractorService, Api>(); ((Api)(object)contractors).State = state;
        context.Services.AddSingleton(shifts); context.Services.AddSingleton(contractors);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        return context;
    }
    public sealed class State { public Guid First = Guid.NewGuid(); public Guid Second = Guid.NewGuid(); public Guid? Selected; public HttpStatusCode? Status; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name.EndsWith("CompanyOptionsAsync")) return State.Status.HasValue
                ? Task.FromException<CompanySelectionOptions>(new HttpRequestException("private diagnostic", null, State.Status))
                : Task.FromResult(new CompanySelectionOptions { CanSelectCompany = true, CompanyId = State.First,
                    Companies = [new() { Id = State.First, Code = "A", Name = "First" }, new() { Id = State.Second, Code = "B", Name = "Second" }] });
            if (method.Name == nameof(IShiftService.GetAllAsync)) return Task.FromResult<IReadOnlyCollection<ShiftResponse>>([]);
            if (method.Name == nameof(IShiftService.CreateAsync))
            {
                State.Selected = ((CreateShiftRequest)args![0]!).CompanyId;
                return Task.FromResult(new ShiftResponse());
            }
            if (method.Name == nameof(IContractorService.CreateContractorAsync))
            {
                State.Selected = ((CreateContractorRequest)args![0]!).CompanyId;
                return Task.FromResult(new ContractorResponse());
            }
            throw new InvalidOperationException("Unexpected method " + method.Name);
        }
    }
    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult<IReadOnlyCollection<string>>(["HRM_Shift_View", "HRM_Shift_Submit"]);
    }
}
