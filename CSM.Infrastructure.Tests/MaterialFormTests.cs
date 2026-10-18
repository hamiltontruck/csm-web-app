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

public sealed class MaterialFormTests
{
    [Fact]
    public void Companyless_admin_without_browser_roles_can_save_selected_company()
    {
        using var context = Context();
        var page = context.RenderComponent<MaterialCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#material-company")));
        Fill(page);
        page.Find("form").Submit();
        var request = Assert.Single(((ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>()).Requests);
        Assert.Equal(CompanyId, JsonSerializer.SerializeToElement(request).GetProperty("CompanyId").GetGuid());
    }

    [Fact]
    public void Api_rejection_is_shown_once_and_entered_fields_are_preserved()
    {
        using var context = Context();
        var proxy = (ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>();
        proxy.Error = "A material with this code already exists.";
        var page = context.RenderComponent<MaterialCreate>();
        Fill(page);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(proxy.Error, Assert.Single(page.FindAll("[role=alert]")).TextContent.Trim()));
        Assert.Equal("MAT-001", page.Find("input").GetAttribute("value"));
        Assert.False(page.Find("button[type=submit]").HasAttribute("disabled"));
    }

    private static void Fill(IRenderedComponent<MaterialCreate> page)
    {
        page.Find("input").Change("MAT-001");
        page.FindAll("input").ToArray()[1].Change("Cement");
        page.FindAll("select").ToArray()[^3].Change("Cement");
        page.FindAll("select").ToArray()[^2].Change("Bag");
        page.Find("input[type=number]").Change("1000");
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
        context.Services.AddSingleton(DispatchProxy.Create<IProcurementService, ProcurementProxy>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    public class AuthProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult<Guid?>(null);
    }

    public class ProcurementProxy : DispatchProxy
    {
        public List<CreateMaterialRequest> Requests { get; } = [];
        public string? Error { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "CreateMaterialAsync")
            {
                Requests.Add((CreateMaterialRequest)args![0]!);
                return Error is null ? Task.FromResult(true) : Task.FromException<bool>(new HttpRequestException(Error));
            }
            var value = method.Name switch
            {
                "GetCategoriesAsync" => "Cement",
                "GetUnitsAsync" => "Bag",
                "GetMaterialTypesAsync" => "Bulk",
                _ => throw new NotSupportedException(method.Name)
            };
            return Task.FromResult(new List<MaterialLookupDto> { new() { Value = value, Name = value } });
        }
    }

    private sealed class Companies : IExecutiveDashboardService
    {
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([new() { Id = CompanyId, Name = "Hamilton" }]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) => throw new NotSupportedException();
    }
}
