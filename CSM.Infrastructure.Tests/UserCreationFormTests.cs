using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Dashboard;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class UserCreationFormTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();

    [Fact]
    public void Companyless_admin_can_choose_company_and_submit_site_assignments()
    {
        using var handler = new Api();
        using var context = Context(handler);
        var page = context.RenderComponent<UserCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#user-company")));
        page.Find("#user-company").Change(CompanyId.ToString());
        Fill(page);
        page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Trim() == "Storekeeper").Change(true);
        page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Contains("Hamilton Test Site")).Change(true);
        page.FindAll("select").Last().Change(SiteId.ToString());
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(handler.Payload));
        var body = handler.Payload!.Value;
        Assert.Equal(CompanyId, body.GetProperty("companyId").GetGuid());
        Assert.Equal(SiteId, body.GetProperty("defaultSiteId").GetGuid());
        Assert.Equal(SiteId, Assert.Single(body.GetProperty("siteIds").EnumerateArray()).GetGuid());
        Assert.Equal("Storekeeper", Assert.Single(body.GetProperty("roles").EnumerateArray()).GetString());
        Assert.NotNull(page.Find("input[type=password]"));
    }

    [Fact]
    public void Company_change_clears_sites_and_default_from_previous_company()
    {
        using var handler = new Api();
        using var context = Context(handler);
        var page = context.RenderComponent<UserCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#user-company")));
        page.Find("#user-company").Change(CompanyId.ToString());
        page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Contains("Hamilton Test Site")).Change(true);
        page.FindAll("select").Last().Change(SiteId.ToString());
        page.Find("#user-company").Change(OtherCompanyId.ToString());
        Assert.DoesNotContain("Hamilton Test Site", page.Markup);
        Assert.DoesNotContain(SiteId.ToString(), page.FindAll("select").Last().OuterHtml);
    }

    [Fact]
    public void Company_admin_uses_authenticated_company_without_company_picker()
    {
        using var handler = new Api();
        using var context = Context(handler, CompanyId);
        var page = context.RenderComponent<UserCreate>();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        Assert.Empty(page.FindAll("#user-company"));
        Fill(page);
        page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Trim() == "Storekeeper").Change(true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(handler.Payload));
        Assert.Equal(CompanyId, handler.Payload!.Value.GetProperty("companyId").GetGuid());
    }

    [Fact]
    public void Missing_company_blocks_submit_and_API_error_preserves_fields()
    {
        using var handler = new Api { Status = HttpStatusCode.BadRequest };
        using var context = Context(handler);
        var page = context.RenderComponent<UserCreate>();
        Fill(page);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Null(handler.Payload);
        page.Find("#user-company").Change(CompanyId.ToString());
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal("Temporary password is not strong enough.",
            Assert.Single(page.FindAll("[role=alert]")).TextContent.Trim()));
        Assert.Equal("storekeeper@example.test", page.FindAll("input:not([type=checkbox])").First().GetAttribute("value"));
        Assert.False(page.Find("button.btn-primary").HasAttribute("disabled"));
    }

    private static void Fill(IRenderedComponent<UserCreate> page)
    {
        page.FindAll("input:not([type=checkbox])").ToArray()[0].Change("storekeeper@example.test");
        page.FindAll("input:not([type=checkbox])").ToArray()[1].Change("Example-Password123!");
        page.FindAll("input:not([type=checkbox])").ToArray()[2].Change("Test");
        page.FindAll("input:not([type=checkbox])").ToArray()[3].Change("Storekeeper");
    }

    private static TestContext Context(Api handler, Guid? company = null)
    {
        var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        ((Auth)(object)auth).Company = company;
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Projects>());
        context.Services.AddSingleton<IExecutiveDashboardService>(new Companies());
        context.Services.AddSingleton(new UserWebService(new HttpClient(handler, false) { BaseAddress = new Uri("https://api.test/api/") }, auth));
        return context;
    }

    public class Auth : DispatchProxy
    {
        public Guid? Company { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "GetCurrentCompanyIdAsync" => Task.FromResult(Company),
            "GetTokenAsync" => Task.FromResult<string?>("test-token"),
            _ => throw new NotSupportedException(method.Name)
        };
    }

    public class Projects : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(new List<SiteResponse> {
            new() { Id = SiteId, CompanyId = CompanyId, Name = "Hamilton Test Site", IsActive = true },
            new() { Id = Guid.NewGuid(), CompanyId = OtherCompanyId, Name = "Other Site", IsActive = true } });
    }

    private sealed class Companies : IExecutiveDashboardService
    {
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([new() { Id = CompanyId, Name = "Hamilton" }, new() { Id = OtherCompanyId, Name = "Other" }]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) => throw new NotSupportedException();
    }

    private sealed class Api : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
        public JsonElement? Payload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Payload = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Status) { Content = JsonContent.Create(new { message = "Temporary password is not strong enough." }) };
        }
    }
}
