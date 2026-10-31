using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Reporting;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class FinancialReportUiScopeTests
{
    [Fact]
    public void Site_scoped_finance_reader_sees_assigned_totals_and_can_choose_only_report_sites()
    {
        using var context = new TestContext();
        context.Services.AddLogging();
        var auth = DispatchProxy.Create<IAuthService, WorkspaceVisibilityTests.Auth>();
        ((WorkspaceVisibilityTests.Auth)(object)auth).Role = "SiteManager";
        ((WorkspaceVisibilityTests.Auth)(object)auth).Permissions = ["Reports_FinancialSummary_View"];
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton(DispatchProxy.Create<IExecutiveDashboardService, Forbidden>());
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Forbidden>());
        context.Services.AddSingleton(DispatchProxy.Create<IReportingService, Report>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var page = context.RenderComponent<Reports>();
        page.WaitForAssertion(() => Assert.Contains("10.00", page.Markup));
        Assert.Contains("Assigned sites", page.Markup);
        Assert.DoesNotContain("Company Summary", page.Markup);
        Assert.DoesNotContain("Executive Report", page.Markup);
        Assert.DoesNotContain("Company-wide", page.Markup);
        page.Find("select").Change("Site");
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("select").Count));
        Assert.Contains("Assigned Test Site", page.FindAll("select")[1].TextContent);
        page.FindAll("select")[1].Change(Report.SiteId.ToString());
        page.WaitForAssertion(() => Assert.Contains("10.00", page.Markup));
        Assert.Empty(page.FindAll(".alert-danger"));
    }
    public class Forbidden : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new InvalidOperationException("Unpermitted lookup must not be requested.");
    }
    public class Report : DispatchProxy
    {
        internal static Guid SiteId = Guid.Parse("d5390f6f-3707-456c-9031-0e4b4498beea");
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IReportingService.GetAllFinancialSummariesAsync) => Task.FromResult(new AllFinancialSummaryResponse { Company = null, Total = new() { TotalBudget = 10 }, Sites = [new() { SiteId = SiteId, SiteName = "Assigned Test Site", SiteCode = "ONE", Financial = new() { TotalBudget = 10 } }] }),
            nameof(IReportingService.GetSiteFinancialSummaryAsync) => Task.FromResult(new FinancialSummaryResponse { TotalBudget = 10 }),
            _ => throw new InvalidOperationException("Unpermitted company or executive report must not be requested.")
        };
    }
}
