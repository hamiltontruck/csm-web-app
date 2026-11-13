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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wage_keeps_typed_text_while_deleting_trailing_zeros_then_accepts_replacement(bool edit)
    {
        using var context = Context(out var state);
        var page = Render(context, edit);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        foreach (var text in new[] { "0.0000", "0.000", "0.00", "0.0", "0", "", "1", "10", "100", "1000" })
        {
            Field(page, "Base Wage *").Input(text);
            Assert.Equal(text, Field(page, "Base Wage *").GetAttribute("value"));
        }
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        var payload = JsonSerializer.SerializeToElement(state.Saved, state.Saved!.GetType());
        Assert.Equal(1000m, payload.GetProperty("BaseWage").GetDecimal());
    }

    [Fact]
    public void Superadmin_company_selection_and_failed_save_preserve_inputs_for_retry()
    {
        using var context=Context(out var state);state.Superadmin=true;
        var page=context.RenderComponent<EmployeeCreate>();page.WaitForAssertion(()=>Assert.Single(page.FindAll("#employee-company")));
        Field(page,"Employee Number *").Change("EMP-LOCAL");Field(page,"First Name *").Change("Comp");Field(page,"Last Name *").Change("Level");Field(page,"Base Wage *").Input("1000.25");
        page.Find("form").Submit();Assert.Equal(0,state.SaveCalls);Assert.Contains("Select a company",page.Find("[role=alert]").TextContent);
        page.Find("#employee-company").Change(state.Company.ToString());state.Fail=true;page.Find("form").Submit();
        page.WaitForAssertion(()=>Assert.Contains("Unable to save employee",page.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("private diagnostic",page.Markup);Assert.Equal("EMP-LOCAL",Field(page,"Employee Number *").GetAttribute("value"));Assert.Equal("1000.25",Field(page,"Base Wage *").GetAttribute("value"));
        Assert.False(page.Find("fieldset").HasAttribute("disabled"));state.Fail=false;page.Find("form").Submit();page.WaitForAssertion(()=>Assert.Equal(2,state.SaveCalls));
        Assert.Equal(state.Company,((CreateEmployeeRequest)state.Saved!).CompanyId);
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

    public sealed class State { public int SaveCalls; public object? Saved; public bool Superadmin,Fail; public Guid Company=Guid.NewGuid(); }
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
                nameof(IEmployeeService.GetCreateOptionsAsync) => Task.FromResult(new EmployeeCreateOptions { CanSelectCompany=State.Superadmin, CompanyId=State.Superadmin?null:State.Company, Companies=State.Superadmin?[new(){Id=State.Company,Code="A",Name="Alpha"},new(){Id=Guid.NewGuid(),Code="B",Name="Beta"}]:[] }),
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
            return State.Fail ? Task.FromException<EmployeeResponse>(new Exception("private diagnostic")) : Task.FromResult(employee);
        }
    }
}
