using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Sites;
using CSM.Web.Models.Users;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class UserEditAssignmentUiTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid Site1 = Guid.NewGuid();
    private static readonly Guid Site2 = Guid.NewGuid();

    [Theory]
    [InlineData("Storekeeper")]
    [InlineData("StoreKeeper")]
    public void Existing_inventory_role_is_checked_and_adding_site_preserves_role_and_default(string role)
    {
        using var api = new Api { Role = role };
        using var context = Context(api);
        var page = context.RenderComponent<UserEdit>(p => p.Add(x => x.Id, UserId));
        page.WaitForAssertion(() => Assert.Contains("Hamilton Transfer Test Site", page.Markup));
        var roleCheckbox = page.FindAll("input[type=checkbox]")
            .Single(x => x.ParentElement!.TextContent.Trim().Equals("Storekeeper", StringComparison.OrdinalIgnoreCase));
        Assert.True(roleCheckbox.HasAttribute("checked"));
        page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Contains("Hamilton Transfer Test Site")).Change(true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(api.SitesPayload));
        Assert.Equal(role, Assert.Single(api.RolesPayload!.Value.GetProperty("roles").EnumerateArray()).GetString());
        Assert.Equal(Site1, api.SitesPayload!.Value.GetProperty("defaultSiteId").GetGuid());
        Assert.Equal(2, api.SitesPayload.Value.GetProperty("siteIds").GetArrayLength());
        Assert.Contains(Site1, api.SitesPayload.Value.GetProperty("siteIds").EnumerateArray().Select(x => x.GetGuid()));
        Assert.Contains(Site2, api.SitesPayload.Value.GetProperty("siteIds").EnumerateArray().Select(x => x.GetGuid()));
    }

    [Fact]
    public void Company_B_user_edit_never_offers_Company_A_sites()
    {
        using var api = new Api { Company = CompanyB };
        using var context = Context(api);
        var page = context.RenderComponent<UserEdit>(p => p.Add(x => x.Id, UserId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.DoesNotContain("Hamilton Test Site", page.Markup);
        Assert.DoesNotContain("Hamilton Transfer Test Site", page.Markup);
    }

    [Fact]
    public void Site_save_validation_is_clear_and_preserves_selected_sites()
    {
        using var api = new Api { RejectSites = true };
        using var context = Context(api);
        var page = context.RenderComponent<UserEdit>(p => p.Add(x => x.Id, UserId));
        page.WaitForAssertion(() => Assert.Contains("Hamilton Transfer Test Site", page.Markup));
        page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Contains("Hamilton Transfer Test Site")).Change(true);
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Selected sites must be active and belong to the user's company.",
            page.Find("[role=alert]").TextContent));
        Assert.True(page.FindAll("input[type=checkbox]").Single(x => x.ParentElement!.TextContent.Contains("Hamilton Transfer Test Site")).HasAttribute("checked"));
        Assert.Single(page.FindAll("form"));
    }

    private static TestContext Context(Api api)
    {
        var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        var http = new HttpClient(api, false) { BaseAddress = new Uri("https://api.test/api/") };
        context.Services.AddSingleton(new UserWebService(http, auth));
        context.Services.AddSingleton(new RoleWebService(http, auth));
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        return context;
    }

    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "GetTokenAsync" => Task.FromResult<string?>("test-token"),
            "ApplyAuthenticationAsync" => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        };
    }

    public class Sites : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> {
                new() { Id = Site1, CompanyId = CompanyA, Name = "Hamilton Test Site", IsActive = true },
                new() { Id = Site2, CompanyId = CompanyA, Name = "Hamilton Transfer Test Site", IsActive = true } });
    }

    private sealed class Api : HttpMessageHandler
    {
        public string Role { get; set; } = "Storekeeper";
        public Guid Company { get; set; } = CompanyA;
        public bool RejectSites { get; set; }
        public JsonElement? RolesPayload { get; private set; }
        public JsonElement? SitesPayload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                if (path.EndsWith("/roles")) return new(HttpStatusCode.OK) {
                    Content = JsonContent.Create(new[] { new { id = Guid.NewGuid(), name = "Storekeeper" } }) };
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new UserModel {
                    Id = UserId, CompanyId = Company, FirstName = "Test", LastName = "Storekeeper",
                    Roles = [Role], IsActive = true, DefaultSiteId = Company == CompanyA ? Site1 : null,
                    SiteAccesses = Company == CompanyA ? [Site1] : [] }) };
            }
            var body = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(token));
            if (path.EndsWith("/roles")) RolesPayload = body;
            if (path.EndsWith("/sites"))
            {
                SitesPayload = body;
                if (RejectSites) return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new {
                    message = "Selected sites must be active and belong to the user's company." }) };
            }
            return new(HttpStatusCode.NoContent);
        }
    }
}
