using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Dashboard;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ReportsCompanySelectionUiTests
{
    [Fact]
    public async Task SuperAdmin_selects_a_company_before_loading_reports()
    {
        using var context = new TestContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        var companies = new CompanyService();
        context.Services.AddSingleton<IExecutiveDashboardService>(companies);
        var reports = DispatchProxy.Create<IReportingService, EmptyResults>();
        context.Services.AddSingleton(reports);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, EmptyResults>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = context.Services.GetRequiredService<ProtectedSessionStorage>();
        await storage.SetAsync("roles", new List<string> { "SuperAdmin" });
        var encrypted = (string)context.JSInterop.Invocations["sessionStorage.setItem"].Last().Arguments[1]!;
        context.JSInterop.Setup<string>("sessionStorage.getItem", "roles").SetResult(encrypted);

        var page = context.RenderComponent<Reports>();

        page.WaitForAssertion(() => Assert.Single(page.FindAll("#reports-company")));
        Assert.Empty(((EmptyResults)(object)reports).Requests);
        page.Find("#reports-company").Change(companies.Id.ToString());
        page.WaitForAssertion(() => Assert.Equal(3, ((EmptyResults)(object)reports).Requests.Count));
        Assert.All(((EmptyResults)(object)reports).Requests, args => Assert.Contains(companies.Id, args));
        Assert.Empty(page.FindAll(".alert-danger"));
    }

    public class EmptyResults : DispatchProxy
    {
        public List<object?[]> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Requests.Add(args ?? []);
            var type = targetMethod!.ReturnType.GenericTypeArguments[0];
            var value = Activator.CreateInstance(type);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type)
                .Invoke(null, [value]);
        }
    }

    private sealed class CompanyService : IExecutiveDashboardService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([new() { Id = Id, Name = "Test Company" }]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) =>
            throw new NotSupportedException();
    }
}
