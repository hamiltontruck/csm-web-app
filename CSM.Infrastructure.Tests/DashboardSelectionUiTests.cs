using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Dashboard;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class DashboardSelectionUiTests
{
    [Fact]
    public async Task SuperAdminCanSelectAndSwitchCompanyDashboards()
    {
        var service = new DashboardStub();
        using var context = await CreateContext(service, "SuperAdmin");
        var page = context.RenderComponent<Dashboard>();
        page.WaitForAssertion(() => Assert.Contains("Select a company", page.Markup));
        Assert.Empty(service.Requests);

        page.Find("select").Change(service.Companies[0].Id.ToString());
        page.WaitForAssertion(() => Assert.Contains("ETB 100", page.Markup));
        Assert.Equal(service.Companies[0].Id, service.Requests.Single());

        page.Find("select").Change(service.Companies[1].Id.ToString());
        page.WaitForAssertion(() => Assert.Contains("ETB 200", page.Markup));
        Assert.Equal(service.Companies[1].Id, service.Requests.Last());
    }

    [Fact]
    public async Task SuperAdminWithoutCompaniesSeesSetupMessage()
    {
        var service = new DashboardStub { Companies = [] };
        using var context = await CreateContext(service, "SuperAdmin");
        var page = context.RenderComponent<Dashboard>();
        page.WaitForAssertion(() => Assert.Contains("No companies are available", page.Find("[role=alert]").TextContent));
        Assert.Empty(service.Requests);
        Assert.DoesNotContain("Loading Executive Dashboard", page.Markup);
    }

    [Fact]
    public async Task CompanyUserLoadsDashboardWithoutCompanyPicker()
    {
        var service = new DashboardStub();
        using var context = await CreateContext(service, "CompanyAdmin");
        var page = context.RenderComponent<Dashboard>();
        page.WaitForAssertion(() => Assert.Contains("Budget overview", page.Markup));
        Assert.Empty(page.FindAll("select"));
        Assert.Null(service.Requests.Single());
    }

    [Fact]
    public async Task Supervisor_sees_project_dashboard_without_financial_cards()
    {
        var service = new DashboardStub();
        using var context = await CreateContext(service, "Supervisor");
        var page = context.RenderComponent<Dashboard>();
        page.WaitForAssertion(() => Assert.Contains("Active projects", page.Markup));
        Assert.Contains("Project progress", page.Markup);
        Assert.DoesNotContain("Budget overview", page.Markup);
        Assert.DoesNotContain("Open Equipment", page.Markup);
        Assert.DoesNotContain("Open Compliance", page.Markup);
        Assert.Single(service.Requests);
    }

    private static async Task<TestContext> CreateContext(DashboardStub service, string role)
    {
        var context = new TestContext();
        context.Services.AddLogging();
        var auth = System.Reflection.DispatchProxy.Create<IAuthService, WorkspaceVisibilityTests.Auth>();
        ((WorkspaceVisibilityTests.Auth)(object)auth).Role = role;
        ((WorkspaceVisibilityTests.Auth)(object)auth).Permissions = role == "Supervisor"
            ? new[] { "Projects_View", "HRM_Payroll_View", "HRM_Attendance_View" }
            : new[] { "Reports_View", "Reports_FinancialSummary_View" };
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<IExecutiveDashboardService>(service);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = context.Services.GetRequiredService<ProtectedSessionStorage>();
        await SeedAsync("jwt", "test-token");
        await SeedAsync("roles", new List<string> { role });
        return context;

        async Task SeedAsync<T>(string key, T value)
        {
            await storage.SetAsync(key, value!);
            var encrypted = (string)context.JSInterop.Invocations["sessionStorage.setItem"].Last().Arguments[1]!;
            context.JSInterop.Setup<string>("sessionStorage.getItem", key).SetResult(encrypted);
        }
    }

    private sealed class DashboardStub : IExecutiveDashboardService
    {
        public List<Guid?> Requests { get; } = [];
        public List<DashboardCompany> Companies { get; init; } =
        [
            new() { Id = Guid.NewGuid(), Name = "First company" },
            new() { Id = Guid.NewGuid(), Name = "Second company" }
        ];
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>(Companies);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null)
        {
            Requests.Add(selectedCompanyId);
            return Task.FromResult<ExecutiveDashboard?>(new()
            {
                TotalBudget = selectedCompanyId == Companies.LastOrDefault()?.Id ? 200 : 100,
                GeneratedAtUtc = DateTime.UtcNow
            });
        }
    }
}
