using System.Reflection;
using Bunit;
using CSM.Domain.Enums;
using CSM.Web.Services;
using CSM.Web.Services.Access;
using CSM.Web.Models.Sites;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SiteManagerFinanceUiTests
{
    [Theory]
    [InlineData("/finance/company")]
    [InlineData("/finance/company/budgets")]
    [InlineData("/finance/company/payments")]
    [InlineData("/finance/company/expenses?siteId=allowed")]
    [InlineData("/finance/company/vendors")]
    [InlineData("/finance/petty-cash")]
    public void Site_manager_cannot_enter_company_finance_even_with_every_grant(string route)
    {
        var permissions = Enum.GetNames<GranularPermission>();
        Assert.False(new WorkspaceAccess(["SiteManager"], permissions).CanVisit(route));
        Assert.True(new WorkspaceAccess(["CompanyAdmin"], permissions).CanVisit(route));
    }

    [Theory]
    [InlineData("/finance")]
    [InlineData("/finance/sites")]
    [InlineData("/finance/sites/11111111-1111-1111-1111-111111111111/budgets")]
    [InlineData("/finance/sites/11111111-1111-1111-1111-111111111111/payments")]
    [InlineData("/finance/sites/11111111-1111-1111-1111-111111111111/expenses")]
    [InlineData("/finance/petty-cash/site/11111111-1111-1111-1111-111111111111")]
    public void Site_finance_navigation_remains_available(string route) =>
        Assert.True(new WorkspaceAccess(["SiteManager"], Enum.GetNames<GranularPermission>()).CanVisit(route));

    [Theory]
    [InlineData("SiteManager", false)]
    [InlineData("SiteManager", true)]
    [InlineData("CompanyAdmin", false)]
    [InlineData("CompanyAdmin", true)]
    public void Both_site_hubs_hide_company_card_for_site_manager_only(string role, bool managementHub)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, [role]);
        var auth = DispatchProxy.Create<IAuthService, WorkspaceVisibilityTests.Auth>();
        ((WorkspaceVisibilityTests.Auth)(object)auth).Role = role;
        ((WorkspaceVisibilityTests.Auth)(object)auth).Permissions = Enum.GetNames<GranularPermission>();
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, SitesApi>());
        IRenderedFragment page = managementHub ? context.RenderComponent<CSM.Web.Components.Pages.Sites>()
            : context.RenderComponent<CSM.Web.Components.Pages.Finance.Sites.Dashboard>();
        page.WaitForAssertion(() => Assert.Contains("Allowed Site", page.Markup));
        if (role == "SiteManager") Assert.DoesNotContain("Company-wide financial records", page.Markup);
        else Assert.Contains("Company-wide financial records", page.Markup);
    }

    public class SitesApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IProjectService.GetSitesAsync)
                ? Task.FromResult(new List<SiteResponse> { new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Allowed Site", Status = "Active" } })
                : throw new InvalidOperationException("Unexpected site request");
    }
}
