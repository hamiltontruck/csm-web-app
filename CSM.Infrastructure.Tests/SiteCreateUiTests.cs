using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Dashboard;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SiteCreateUiTests
{
    [Fact]
    public async Task Empty_site_code_is_shown_as_validation_without_an_api_request()
    {
        using var context = await CreateContext();
        var page = context.RenderComponent<SiteCreate>();
        await page.Find("button.btn-primary").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Contains("Site code is required", page.Find("[role=alert]").TextContent));
        Assert.Empty(((ProjectProxy)(object)context.Services.GetRequiredService<IProjectService>()).Requests);
    }

    [Fact]
    public async Task Save_uses_the_selected_company_and_entered_site_fields()
    {
        using var context = await CreateContext();
        var page = context.RenderComponent<SiteCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#site-company")));
        page.Find("#site-company").Change(CompanyId.ToString());
        var inputs = page.FindAll("input");
        inputs[0].Change("New Test Site");
        inputs[1].Change("TEST-001");
        await page.Find("button.btn-primary").ClickAsync(new());
        var request = Assert.Single(((ProjectProxy)(object)context.Services.GetRequiredService<IProjectService>()).Requests);
        Assert.Equal(CompanyId, request.CompanyId);
        Assert.Equal("TEST-001", request.SiteCode);
        Assert.Equal("New Test Site", request.Name);
    }

    [Fact]
    public async Task Api_save_failure_keeps_the_form_and_shows_a_recoverable_error()
    {
        using var context = await CreateContext();
        var proxy = (ProjectProxy)(object)context.Services.GetRequiredService<IProjectService>();
        proxy.Error = "A site with this code already exists in the company.";
        var page = context.RenderComponent<SiteCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#site-company")));
        page.Find("#site-company").Change(CompanyId.ToString());
        var inputs = page.FindAll("input");
        inputs[0].Change("New Test Site");
        inputs[1].Change("TEST-001");
        await page.Find("button.btn-primary").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Contains(proxy.Error, page.Find("[role=alert]").TextContent));
        Assert.False(page.Find("button.btn-primary").HasAttribute("disabled"));
        Assert.Equal("TEST-001", page.FindAll("input")[1].GetAttribute("value"));
    }

    private static readonly Guid CompanyId = Guid.NewGuid();

    private static async Task<TestContext> CreateContext()
    {
        var context = new TestContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<IExecutiveDashboardService>(new Companies());
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, AuthProxy>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = context.Services.GetRequiredService<ProtectedSessionStorage>();
        await storage.SetAsync("roles", new List<string> { "SuperAdmin" });
        var encrypted = (string)context.JSInterop.Invocations["sessionStorage.setItem"].Last().Arguments[1]!;
        context.JSInterop.Setup<string>("sessionStorage.getItem", "roles").SetResult(encrypted);
        return context;
    }

    public class ProjectProxy : DispatchProxy
    {
        public List<CreateSiteRequest> Requests { get; } = [];
        public string? Error { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "CreateSiteAsync")
            {
                Requests.Add((CreateSiteRequest)args![0]!);
                return Error is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Error));
            }
            throw new NotSupportedException(targetMethod.Name);
        }
    }

    public class AuthProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Task.FromResult<Guid?>(CompanyId);
    }

    private sealed class Companies : IExecutiveDashboardService
    {
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([new() { Id = CompanyId, Name = "Real Company" }]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) =>
            throw new NotSupportedException();
    }
}
