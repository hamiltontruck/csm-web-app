using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class CompanyManagementUiTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.CompanyAdmin, false)]
    [InlineData(AppRoles.Administrator, false)]
    [InlineData(AppRoles.Storekeeper, false)]
    public async Task Company_endpoints_require_SuperAdmin(string role, bool allowed)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy(AppPolicies.CompanyManagement,
            policy => policy.RequireRole(AppPolicies.CompanyManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var attributes = typeof(CompaniesController).GetCustomAttributes<AuthorizeAttribute>();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        Assert.Equal(allowed, (await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, null, policy!)).Succeeded);
    }

    [Fact]
    public void SuperAdmin_lists_companies_and_creates_company_with_ETB_default()
    {
        using var api = new Api();
        using var context = Context(api);
        var page = Render(context);
        page.WaitForAssertion(() => Assert.Contains("Hamilton Business Group", page.Markup));
        page.Find("#new-company").Click();
        Assert.Equal("ETB", page.Find("#company-currency").GetAttribute("value"));
        page.Find("#company-code").Change("HBG-TEST-B");
        page.Find("#company-name").Change("HBG Access Test Company B");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("HBG Access Test Company B", page.Markup));
        Assert.NotNull(api.Payload);
        Assert.Equal("HBG-TEST-B", api.Payload!.Value.GetProperty("code").GetString());
        Assert.Equal("ETB", api.Payload.Value.GetProperty("baseCurrencyCode").GetString());
        Assert.Equal(1, api.Posts);
        Assert.Empty(page.FindAll("form"));
    }

    [Fact]
    public void Duplicate_code_keeps_form_and_values_with_one_clear_error()
    {
        using var api = new Api { Duplicate = true };
        using var context = Context(api);
        var page = Render(context);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#new-company")));
        page.Find("#new-company").Click();
        page.Find("#company-code").Change("HBG-TEST-B");
        page.Find("#company-name").Change("Company B");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal("A company with this code already exists.",
            page.Find("[role=alert]").TextContent.Trim()));
        Assert.Single(page.FindAll("[role=alert]"));
        Assert.Equal("HBG-TEST-B", page.Find("#company-code").GetAttribute("value"));
        Assert.Equal("Company B", page.Find("#company-name").GetAttribute("value"));
        Assert.False(page.Find("#save-company").HasAttribute("disabled"));
    }

    [Fact]
    public void Required_fields_prevent_empty_company_creation()
    {
        using var api = new Api();
        using var context = Context(api);
        var page = Render(context);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#new-company")));
        page.Find("#new-company").Click();
        page.Find("form").Submit();
        Assert.Equal(0, api.Posts);
        Assert.Contains("Company code is required.", page.Markup);
        Assert.Contains("Company name is required.", page.Markup);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "You do not have permission to manage companies.")]
    [InlineData(HttpStatusCode.Unauthorized, "Please sign in again.")]
    [InlineData(HttpStatusCode.InternalServerError, "temporarily unavailable")]
    public void Failed_company_access_shows_safe_feedback_without_create_form(HttpStatusCode status, string message)
    {
        using var api = new Api { Status = status };
        using var context = Context(api);
        var page = Render(context);
        page.WaitForAssertion(() => Assert.Contains(message, page.Find("[role=alert]").TextContent));
        Assert.Empty(page.FindAll("#new-company"));
        Assert.Empty(page.FindAll("form"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Storekeeper", false)]
    [InlineData("CompanyAdmin", false)]
    public void Companies_menu_is_visible_only_for_SuperAdmin(string role, bool visible)
    {
        using var api = new Api { Role = role };
        using var context = Context(api);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        context.Services.AddSingleton(DispatchProxy.Create<INotificationApiService, Notifications>());
        var menu = context.RenderComponent<CSM.Web.Components.Layout.NavMenu>();
        menu.WaitForAssertion(() => Assert.Equal(visible, menu.FindAll("a[href='/companies']").Count == 1));
        if (visible) Assert.Contains("Companies", menu.Markup);
    }

    public class Notifications : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(0);
    }

    private static TestContext Context(Api api)
    {
        var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        var type = typeof(UserWebService).Assembly.GetType("CSM.Web.Services.CompanyWebService");
        Assert.NotNull(type);
        context.Services.AddSingleton(type!, Activator.CreateInstance(type!,
            new HttpClient(api, false) { BaseAddress = new Uri("https://api.test/api/") }, auth)!);
        return context;
    }

    private static IRenderedComponent<DynamicComponent> Render(TestContext context)
    {
        var page = typeof(UserWebService).Assembly.GetType("CSM.Web.Components.Pages.Companies");
        Assert.NotNull(page);
        return context.RenderComponent<DynamicComponent>(p => p.Add(x => x.Type, page!));
    }

    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name switch
            {
                "GetTokenAsync" => Task.FromResult<string?>("test-token"),
                "GetPermissionsAsync" => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>()),
                _ => throw new NotSupportedException(method.Name)
            };
    }

    private sealed class Api : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool Duplicate { get; set; }
        public string Role { get; set; } = "SuperAdmin";
        public int Posts { get; private set; }
        public JsonElement? Payload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/me"))
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { roles = new[] { Role } }) };
            if (request.Method == HttpMethod.Post)
            {
                Posts++;
                Payload = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(token));
                if (Duplicate) return new(HttpStatusCode.BadRequest) {
                    Content = JsonContent.Create(new { message = "A company with this code already exists." }) };
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(new {
                    id = Guid.NewGuid(), code = Payload.Value.GetProperty("code").GetString(),
                    name = Payload.Value.GetProperty("name").GetString(), baseCurrencyCode = "ETB", isActive = true }) };
            }
            return new(Status) { Content = Status == HttpStatusCode.OK ?
                JsonContent.Create(new[] { new { id = Guid.NewGuid(), code = "HBG", name = "Hamilton Business Group",
                    baseCurrencyCode = "ETB", isActive = true } }) : new StringContent("private diagnostic") };
        }
    }
}
