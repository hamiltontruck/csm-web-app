using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.Finance.Company;
using CSM.Web.Models.Dashboard;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class CompanyBudgetFormTests
{
    [Fact]
    public async Task Api_rejection_stays_in_form_and_preserves_entered_budget()
    {
        using var context = await CreateContext();
        var service = (FinanceProxy)(object)context.Services.GetRequiredService<IFinanceService>();
        service.Error = "Budget number already exists.";
        var page = context.RenderComponent<BudgetCreateCompany>();
        Fill(page);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains(service.Error, page.Find("[role=alert]").TextContent));
        Assert.Equal("HBG-BUD-001", page.Find("input").GetAttribute("value"));
        Assert.False(page.Find("button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Company_selection_is_included_in_saved_budget()
    {
        using var context = await CreateContext();
        var page = context.RenderComponent<BudgetCreateCompany>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#budget-company")));
        page.Find("#budget-company").Change(CompanyId.ToString());
        Fill(page);
        page.Find("form").Submit();
        var request = Assert.Single(((FinanceProxy)(object)context.Services.GetRequiredService<IFinanceService>()).Requests);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(request);
        Assert.Equal(CompanyId, json.GetProperty("CompanyId").GetGuid());
    }

    private static void Fill(IRenderedComponent<BudgetCreateCompany> page)
    {
        page.Find("input").Change("HBG-BUD-001");
        page.FindAll("input").ToArray()[1].Change("Test Budget");
        page.FindAll("input").ToArray()[3].Change("100000");
        page.Find("input[type=date]").Change("2026-10-18");
    }

    private static readonly Guid CompanyId = Guid.NewGuid();

    private static async Task<TestContext> CreateContext()
    {
        var context = new TestContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<IExecutiveDashboardService>(new Companies());
        context.Services.AddSingleton(DispatchProxy.Create<IFinanceService, FinanceProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, SiteCreateUiTests.AuthProxy>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = context.Services.GetRequiredService<ProtectedSessionStorage>();
        await storage.SetAsync("roles", new List<string> { "SuperAdmin" });
        var encrypted = (string)context.JSInterop.Invocations["sessionStorage.setItem"].Last().Arguments[1]!;
        context.JSInterop.Setup<string>("sessionStorage.getItem", "roles").SetResult(encrypted);
        return context;
    }

    public class FinanceProxy : DispatchProxy
    {
        public List<CreateBudgetModel> Requests { get; } = [];
        public string? Error { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "CreateBudgetAsync")
            {
                Requests.Add((CreateBudgetModel)args![0]!);
                return Error is null ? Task.CompletedTask : Task.FromException(new HttpRequestException(Error));
            }
            throw new NotSupportedException(targetMethod.Name);
        }
    }

    private sealed class Companies : IExecutiveDashboardService
    {
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([new() { Id = CompanyId, Name = "Real Company" }]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) => throw new NotSupportedException();
    }
}
