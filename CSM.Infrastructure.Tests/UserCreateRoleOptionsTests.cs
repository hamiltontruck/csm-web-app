using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Services;
using CSM.Web.Models.Sites;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class UserCreateRoleOptionsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Creation_uses_server_roles_and_blocks_submission_when_options_fail(bool fail)
    {
        using var context = new TestContext();
        using var api = new Api(fail);
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(new UserWebService(new HttpClient(api) { BaseAddress = new("https://api.test/api/") }, auth));
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        context.Services.AddSingleton(DispatchProxy.Create<IExecutiveDashboardService, Dashboard>());
        var page = context.RenderComponent<UserCreate>();
        page.WaitForAssertion(() => Assert.True(api.Requested));
        if (fail)
        {
            page.WaitForAssertion(() => Assert.Contains("Unable to load", page.Find("[role=alert]").TextContent));
            Assert.True(page.Find("button.btn-primary").HasAttribute("disabled"));
            Assert.Single(page.FindAll("button[data-retry-role-options]"));
        }
        else
        {
            page.WaitForAssertion(() => Assert.Contains("FinanceOfficer", page.Markup));
            Assert.DoesNotContain("EquipmentManager", page.Markup);
            Assert.DoesNotContain("Administrator</label>", page.Markup);
            Assert.Contains("ServerRole", page.Markup);
        }
    }
    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "GetTokenAsync" => Task.FromResult<string?>("test"),
            "GetCurrentCompanyIdAsync" => Task.FromResult<Guid?>(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public class Sites : DispatchProxy { protected override object? Invoke(MethodInfo? m, object?[]? a) => Task.FromResult(new List<SiteResponse>()); }
    public class Dashboard : DispatchProxy { protected override object? Invoke(MethodInfo? m, object?[]? a) => throw new InvalidOperationException("Company-bound user needs no company list."); }
    private sealed class Api(bool fail) : HttpMessageHandler
    {
        public bool Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.EndsWith("/users/role-options", request.RequestUri!.AbsolutePath); Requested = true;
            return Task.FromResult(new HttpResponseMessage(fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(new[] { "FinanceOfficer", "ServerRole" }) });
        }
    }
}
