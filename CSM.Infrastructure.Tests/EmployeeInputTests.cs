using System.Reflection;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EmployeeInputTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Save_sends_current_job_title_and_wage_with_or_without_blur(bool edit, bool inputOnly)
    {
        using var context = Context(out var state);
        var page = Render(context, edit);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        if (!edit) Field(page, "Employee Number *").Change("EMP-HR-TEST-001");
        Field(page, "First Name *").Change("Test");
        Field(page, "Last Name *").Change("HR Employee");
        page.FindAll("select").ElementAt(0).Change("Permanent");
        page.FindAll("select").ElementAt(1).Change("Company");
        page.FindAll("select").ElementAt(2).Change("Monthly");
        if (inputOnly)
        {
            Field(page, "Job Title").Input("Test Worker");
            Field(page, "Base Wage *").Input("1000.25");
        }
        else
        {
            Field(page, "Job Title").Change("Test Worker");
            Field(page, "Base Wage *").Change("1000.25");
        }
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        var payload = JsonSerializer.SerializeToElement(state.Saved, state.Saved!.GetType());
        Assert.Equal("Test Worker", payload.GetProperty("JobTitle").GetString());
        Assert.Equal(1000.25m, payload.GetProperty("BaseWage").GetDecimal());
        Assert.Equal("Company", payload.GetProperty("EmployeeScope").GetString());
        Assert.Equal("Monthly", payload.GetProperty("WageType").GetString());
        Assert.Equal("ETB", payload.GetProperty("CurrencyCode").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_wage_blocks_submit_until_corrected_without_reusing_old_wage(bool edit)
    {
        using var context = Context(out var state);
        var page = Render(context, edit);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Field(page, "Base Wage *").Input("");
        page.Find("form").Submit();
        Assert.Equal(0, state.SaveCalls);
        Assert.Contains("must be a number", page.Markup);
        Field(page, "Base Wage *").Input("900.50");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        var payload = JsonSerializer.SerializeToElement(state.Saved, state.Saved!.GetType());
        Assert.Equal(900.50m, payload.GetProperty("BaseWage").GetDecimal());
    }

    private static IElement Field(IRenderedFragment page, string label) => page.FindAll(".col-md-4")
        .Single(x => x.QuerySelector("label")?.TextContent.Trim() == label).QuerySelector("input")!;

    private static IRenderedFragment Render(TestContext context, bool edit) => edit
        ? context.RenderComponent<EmployeeEdit>(p => p.Add(x => x.EmployeeId, Guid.Parse("11111111-1111-1111-1111-111111111111")))
        : context.RenderComponent<EmployeeCreate>();

    private static TestContext Context(out State state)
    {
        var context = new TestContext();
        state = new State();
        var service = DispatchProxy.Create<IEmployeeService, Api>();
        ((Api)(object)service).State = state;
        context.Services.AddSingleton(service);
        return context;
    }

    public sealed class State { public int SaveCalls; public object? Saved; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var employee = new EmployeeResponse
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                EmployeeNumber = "EMP-HR-TEST-001", FirstName = "Test", LastName = "HR Employee",
                HireDate = new DateOnly(2026, 11, 3), EmployeeType = "Permanent", EmployeeScope = "Company",
                WageType = "Monthly", CurrencyCode = "ETB", BaseWage = 75m, JobTitle = "Old title", Status = "Active"
            };
            return method!.Name switch
            {
                nameof(IEmployeeService.GetEmployeesAsync) => Task.FromResult<IReadOnlyList<EmployeeResponse>>([employee]),
                nameof(IEmployeeService.GetEmployeeAsync) => Task.FromResult(employee),
                nameof(IEmployeeService.CreateEmployeeAsync) => Save(args![0], employee),
                nameof(IEmployeeService.UpdateEmployeeAsync) => Save(args![1], employee),
                _ => throw new InvalidOperationException("Unexpected employee operation")
            };
        }
        private Task<EmployeeResponse> Save(object? request, EmployeeResponse employee)
        {
            State.SaveCalls++;
            State.Saved = request;
            return Task.FromResult(employee);
        }
    }
}
