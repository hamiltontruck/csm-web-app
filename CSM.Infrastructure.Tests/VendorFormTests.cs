using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Dashboard;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class VendorFormTests
{
    [Fact]
    public void Companyless_admin_without_browser_roles_can_save_selected_company()
    {
        using var context = Context();
        var page = context.RenderComponent<VendorCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#vendor-company")));
        Fill(page);
        page.Find("button.btn-primary").Click();
        var request = Assert.Single(((VendorProxy)(object)context.Services.GetRequiredService<IVendorService>()).Requests);
        Assert.Equal(CompanyId, JsonSerializer.SerializeToElement(request).GetProperty("CompanyId").GetGuid());
    }

    [Fact]
    public void Api_rejection_is_shown_once_and_entered_fields_are_preserved()
    {
        using var context = Context();
        var proxy = (VendorProxy)(object)context.Services.GetRequiredService<IVendorService>();
        proxy.Error = "A vendor with this code already exists.";
        var page = context.RenderComponent<VendorCreate>();
        Fill(page);
        page.Find("button.btn-primary").Click();
        page.WaitForAssertion(() => Assert.Equal(proxy.Error, Assert.Single(page.FindAll("[role=alert]")).TextContent.Trim()));
        Assert.Equal("VEN-001", page.Find("input").GetAttribute("value"));
        Assert.False(page.Find("button.btn-primary").HasAttribute("disabled"));
    }

    private static void Fill(IRenderedComponent<VendorCreate> page)
    {
        page.Find("input").Input("VEN-001");
        page.FindAll("input").ToArray()[1].Input("Test Cement Supplier");

    }

    private static readonly Guid CompanyId = Guid.NewGuid();

    private static TestContext Context()
    {
        var context = new TestContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<IExecutiveDashboardService>(new Companies());
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, AuthProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, VendorProxy>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    public class AuthProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult<Guid?>(null);
    }

    public class VendorProxy : DispatchProxy
    {
        public List<CreateVendorRequest> Requests { get; } = [];
        public string? Error { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "CreateVendorAsync")
            {
                Requests.Add((CreateVendorRequest)args![0]!);
                return Error is null ? Task.FromResult(true) : Task.FromException<bool>(new HttpRequestException(Error));
            }
            throw new NotSupportedException(method.Name);
        }
    }

    private sealed class Companies : IExecutiveDashboardService
    {
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([new() { Id = CompanyId, Name = "Hamilton" }]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) => throw new NotSupportedException();
    }
}
